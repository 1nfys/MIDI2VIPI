using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System;

namespace MIDI2VIPI
{
    internal class MidiEvent
    {
        public long Tick;
        public int Track;
        public bool IsMeta;
        public byte Status;
        public byte Data1;
        public byte Data2;
        public byte MetaType;
        public byte[] MetaData;
    }

    internal class SimpleMidiParser
    {
        public int TicksPerQuarterNote;
        public List<MidiEvent>[] Tracks;

        private int ReadVLQ(BinaryReader br)
        {
            int value = 0;
            byte b;
            do
            {
                b = br.ReadByte();
                value = (value << 7) | (b & 0x7F);
            }
            while ((b & 0x80) != 0);
            return value;
        }

        public SimpleMidiParser(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                if (new string(br.ReadChars(4)) != "MThd")
                    throw new Exception("Not a valid MIDI file.");

                br.ReadInt32();
                int format = (br.ReadByte() << 8) | br.ReadByte();
                int trackCount = (br.ReadByte() << 8) | br.ReadByte();
                TicksPerQuarterNote = (br.ReadByte() << 8) | br.ReadByte();

                Tracks = new List<MidiEvent>[trackCount];

                for (int i = 0; i < trackCount; i++)
                {
                    Tracks[i] = new List<MidiEvent>();
                    string chunkType = new string(br.ReadChars(4));

                    while (chunkType != "MTrk")
                    {
                        int skipBytes = (br.ReadByte() << 24) | (br.ReadByte() << 16) | (br.ReadByte() << 8) | br.ReadByte();
                        fs.Position += skipBytes;
                        if (fs.Position >= fs.Length) break;
                        chunkType = new string(br.ReadChars(4));
                    }

                    if (chunkType != "MTrk") continue;

                    int trackLen = (br.ReadByte() << 24) | (br.ReadByte() << 16) | (br.ReadByte() << 8) | br.ReadByte();
                    long trackEnd = fs.Position + trackLen;
                    long currentTick = 0;
                    byte runningStatus = 0;

                    while (fs.Position < trackEnd)
                    {
                        currentTick += ReadVLQ(br);
                        byte statusByte = br.ReadByte();

                        if (statusByte < 0x80)
                        {
                            statusByte = runningStatus;
                            fs.Position--;
                        }
                        else if (statusByte < 0xFF)
                        {
                            runningStatus = statusByte;
                        }

                        if (statusByte == 0xFF)
                        {
                            byte metaType = br.ReadByte();
                            int metaLen = ReadVLQ(br);
                            byte[] metaData = br.ReadBytes(metaLen);

                            Tracks[i].Add(new MidiEvent
                            {
                                Tick = currentTick,
                                Track = i,
                                IsMeta = true,
                                MetaType = metaType,
                                MetaData = metaData
                            });
                        }
                        else if (statusByte == 0xF0 || statusByte == 0xF7)
                        {
                            int sysExLen = ReadVLQ(br);
                            fs.Position += sysExLen;
                        }
                        else
                        {
                            byte cmd = (byte)(statusByte & 0xF0);
                            byte data1 = br.ReadByte();
                            byte data2 = 0;

                            if (cmd != 0xC0 && cmd != 0xD0)
                            {
                                data2 = br.ReadByte();
                            }

                            Tracks[i].Add(new MidiEvent
                            {
                                Tick = currentTick,
                                Track = i,
                                Status = statusByte,
                                Data1 = data1,
                                Data2 = data2
                            });
                        }
                    }
                }
            }
        }
    }

    internal class NativeMidiOut : IDisposable
    {
        private IntPtr handle;

        [DllImport("winmm.dll")]
        private static extern uint midiOutOpen(out IntPtr lphmo, uint uDeviceID, IntPtr dwCallback, IntPtr dwCallbackInstance, uint dwFlags);

        [DllImport("winmm.dll")]
        private static extern uint midiOutShortMsg(IntPtr hmo, uint dwMsg);

        [DllImport("winmm.dll")]
        private static extern uint midiOutClose(IntPtr hmo);

        public NativeMidiOut(uint device = 0)
        {
            midiOutOpen(out handle, device, IntPtr.Zero, IntPtr.Zero, 0);
        }

        public void Send(int msg)
        {
            if (handle != IntPtr.Zero)
            {
                midiOutShortMsg(handle, (uint)msg);
            }
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                midiOutClose(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    internal class MidiEngine
    {
        public struct TrackInfo
        {
            public int Index;
            public string Name;
            public int NoteCount;
            public HashSet<int> Programs;

            public string InstrDesc
            {
                get
                {
                    if (Programs == null || Programs.Count == 0) return "Percussion";
                    var sorted = Programs.OrderBy(p => p).Select(p => "Instr " + p);
                    return string.Join(", ", sorted);
                }
            }
        }

        private class PlayNote
        {
            public double T;
            public bool On;
            public int Note;
            public int Vel;
            public int Ch;
        }

        private const string VirtualPianoMap = "1!2@34$5%6^78*9(0qQwWeErtTyYuiIoOpPasSdDfgGhHjJklLzZxcCvVbBnm";
        private static readonly Dictionary<int, char> KeyMap;

        public Action<double, double> OnProgress;
        public Action OnFinish;

        public volatile bool Playing;
        public volatile bool Paused;
        public volatile bool Macro;
        public volatile int Bpm = 120;
        public volatile int Transpose;

        public double Volume = 1.0;
        public double Duration;
        public double SeekReq = -1.0;
        public int InitialBpm = 120;

        private List<PlayNote> _queue = new List<PlayNote>();
        private Thread _playbackThread;
        private volatile bool _stopFlag;
        private NativeMidiOut _midiOut;

        static MidiEngine()
        {
            KeyMap = new Dictionary<int, char>();
            for (int i = 0; i < VirtualPianoMap.Length; i++)
            {
                KeyMap[i + 36] = VirtualPianoMap[i];
            }
        }

        public MidiEngine()
        {
            try
            {
                _midiOut = new NativeMidiOut();
            }
            catch { }
        }

        public static List<TrackInfo> ParseMeta(string path, out int bpm)
        {
            var parser = new SimpleMidiParser(path);
            bpm = 120;

            foreach (var track in parser.Tracks)
            {
                var tempoEvent = track.FirstOrDefault(e => e.IsMeta && e.MetaType == 0x51);
                if (tempoEvent != null)
                {
                    int microseconds = (tempoEvent.MetaData[0] << 16) | (tempoEvent.MetaData[1] << 8) | tempoEvent.MetaData[2];
                    bpm = (int)Math.Round(60000000.0 / microseconds);
                    break;
                }
            }

            var infos = new List<TrackInfo>();
            for (int i = 0; i < parser.Tracks.Length; i++)
            {
                var trackInfo = new TrackInfo
                {
                    Index = i,
                    Name = "Track " + i,
                    NoteCount = 0,
                    Programs = new HashSet<int>()
                };

                var activePrograms = new Dictionary<int, int>();

                foreach (var ev in parser.Tracks[i])
                {
                    if (ev.IsMeta && ev.MetaType == 0x03)
                    {
                        trackInfo.Name = Encoding.Default.GetString(ev.MetaData);
                    }
                    else if (!ev.IsMeta)
                    {
                        int cmd = ev.Status & 0xF0;
                        int ch = ev.Status & 0x0F;

                        if (cmd == 0xC0)
                        {
                            activePrograms[ch] = ev.Data1;
                        }
                        else if (cmd == 0x90 && ev.Data2 > 0)
                        {
                            trackInfo.NoteCount++;
                            int program;
                            trackInfo.Programs.Add(activePrograms.TryGetValue(ch, out program) ? program : 0);
                        }
                    }
                }

                if (trackInfo.NoteCount > 0)
                {
                    infos.Add(trackInfo);
                }
            }
            return infos;
        }

        public bool Load(string path, HashSet<int> tracks, int targetBpm)
        {
            Stop();
            Bpm = targetBpm;
            _queue.Clear();

            try
            {
                var parser = new SimpleMidiParser(path);
                int tpb = parser.TicksPerQuarterNote;

                var tempos = new List<KeyValuePair<long, double>>();
                var allEvents = new List<Tuple<int, long, MidiEvent>>();

                for (int i = 0; i < parser.Tracks.Length; i++)
                {
                    foreach (var ev in parser.Tracks[i])
                    {
                        allEvents.Add(Tuple.Create(i, ev.Tick, ev));
                        if (ev.IsMeta && ev.MetaType == 0x51)
                        {
                            int microseconds = (ev.MetaData[0] << 16) | (ev.MetaData[1] << 8) | ev.MetaData[2];
                            tempos.Add(new KeyValuePair<long, double>(ev.Tick, microseconds));
                        }
                    }
                }

                tempos.Sort((a, b) => a.Key.CompareTo(b.Key));
                InitialBpm = (tempos.Count > 0) ? (int)Math.Round(60000000.0 / tempos[0].Value) : 120;

                Func<long, double> ticksToSeconds = delegate (long ticks)
                {
                    if (tempos.Count == 0)
                        return (double)ticks * 60.0 / 120.0 / tpb;

                    double currentTempo = 500000.0;
                    double accumulatedSeconds = 0.0;
                    long lastTick = 0;

                    foreach (var tempo in tempos)
                    {
                        if (ticks < tempo.Key) break;
                        accumulatedSeconds += (double)(tempo.Key - lastTick) * currentTempo / 1000000.0 / tpb;
                        currentTempo = tempo.Value;
                        lastTick = tempo.Key;
                    }
                    return accumulatedSeconds + (double)(ticks - lastTick) * currentTempo / 1000000.0 / tpb;
                };

                allEvents.Sort((a, b) => a.Item2.CompareTo(b.Item2));

                foreach (var tuple in allEvents)
                {
                    if (!tracks.Contains(tuple.Item1)) continue;

                    var ev = tuple.Item3;
                    if (ev.IsMeta) continue;

                    int cmd = ev.Status & 0xF0;
                    int ch = ev.Status & 0x0F;

                    if (cmd == 0x90)
                    {
                        bool isOn = ev.Data2 > 0;
                        _queue.Add(new PlayNote
                        {
                            T = ticksToSeconds(tuple.Item2),
                            On = isOn,
                            Note = ev.Data1,
                            Vel = isOn ? ev.Data2 : 0,
                            Ch = ch
                        });
                    }
                    else if (cmd == 0x80)
                    {
                        _queue.Add(new PlayNote
                        {
                            T = ticksToSeconds(tuple.Item2),
                            On = false,
                            Note = ev.Data1,
                            Vel = 0,
                            Ch = ch
                        });
                    }
                }

                if (_queue.Count == 0) return false;

                Duration = _queue.Last().T;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Start()
        {
            if (_queue.Count == 0) return;

            if (Playing)
            {
                Paused = false;
                return;
            }

            if (_midiOut == null)
            {
                try { _midiOut = new NativeMidiOut(); } catch { }
            }

            Playing = true;
            Paused = false;
            _stopFlag = false;

            _playbackThread = new Thread(PlaybackLoop) { IsBackground = true };
            _playbackThread.Start();
        }

        public void TogglePause()
        {
            if (Playing)
            {
                Paused = !Paused;
            }
        }

        public void Stop()
        {
            _stopFlag = true;
            Playing = false;
            Paused = false;

            if (_playbackThread != null)
            {
                var th = _playbackThread;
                _playbackThread = null;
                th.Join(500);
            }

            Silence();
            if (OnProgress != null)
            {
                OnProgress(0.0, 0.0);
            }
        }

        public void Cleanup()
        {
            Stop();
            if (_midiOut != null)
            {
                try { _midiOut.Dispose(); } catch { }
                _midiOut = null;
            }
        }

        private void Silence()
        {
            if (_midiOut == null) return;
            try
            {
                for (int i = 0; i < 16; i++)
                {
                    _midiOut.Send(0xB0 | i | 0x7B00);
                }
            }
            catch { }
        }

        private static char? MapKey(int note, int shift)
        {
            int shiftedNote = note + shift;
            if (shiftedNote < 36)
            {
                shiftedNote += (36 - shiftedNote + 11) / 12 * 12;
            }
            else if (shiftedNote > 96)
            {
                shiftedNote -= (shiftedNote - 96 + 11) / 12 * 12;
            }

            char keyChar;
            return KeyMap.TryGetValue(shiftedNote, out keyChar) ? (char?)keyChar : null;
        }

        private void PlaybackLoop()
        {
            int totalNotes = _queue.Count;
            double currentElapsed = 0.0;
            double lastReportedProgress = 0.0;
            int eventIndex = 0;

            var stopwatch = Stopwatch.StartNew();
            double prevSeconds = stopwatch.Elapsed.TotalSeconds;

            while (eventIndex < totalNotes && !_stopFlag)
            {

                if (SeekReq >= 0.0)
                {
                    currentElapsed = SeekReq;
                    SeekReq = -1.0;
                    Silence();

                    eventIndex = -1;
                    for (int i = 0; i < totalNotes; i++)
                    {
                        if (_queue[i].T >= currentElapsed)
                        {
                            eventIndex = i;
                            break;
                        }
                    }
                    if (eventIndex < 0) eventIndex = totalNotes;

                    prevSeconds = stopwatch.Elapsed.TotalSeconds;
                    if (OnProgress != null)
                    {
                        OnProgress(Math.Min(currentElapsed, Duration), Duration);
                    }
                    lastReportedProgress = currentElapsed;

                    if (eventIndex >= totalNotes) break;
                }

                while (Paused && !_stopFlag && SeekReq < 0.0)
                {
                    Thread.Sleep(20);
                    prevSeconds = stopwatch.Elapsed.TotalSeconds;
                }

                if (_stopFlag) break;
                if (SeekReq >= 0.0) continue;

                double now = stopwatch.Elapsed.TotalSeconds;
                double deltaTime = now - prevSeconds;
                prevSeconds = now;

                double speedFactor = (InitialBpm > 0) ? ((double)Bpm / InitialBpm) : 1.0;
                currentElapsed += deltaTime * speedFactor;

                double targetTime = _queue[eventIndex].T;
                while (currentElapsed < targetTime && !_stopFlag && SeekReq < 0.0)
                {
                    double remainingRealTime = (targetTime - currentElapsed) / (speedFactor > 0 ? speedFactor : 1.0);

                    if (remainingRealTime > 0.015)
                    {
                        Thread.Sleep((int)Math.Min(50.0, (remainingRealTime - 0.01) * 1000.0));
                    }
                    else
                    {
                        Thread.Sleep(0);
                    }

                    now = stopwatch.Elapsed.TotalSeconds;
                    deltaTime = now - prevSeconds;
                    prevSeconds = now;

                    speedFactor = (InitialBpm > 0) ? ((double)Bpm / InitialBpm) : 1.0;
                    currentElapsed += deltaTime * speedFactor;
                }

                if (_stopFlag) break;
                if (SeekReq >= 0.0) continue;

                var playNote = _queue[eventIndex];

                if (_midiOut != null && !Macro)
                {
                    try
                    {
                        int scaledVelocity = (int)(playNote.Vel * Volume);
                        int finalNote = Math.Max(0, Math.Min(127, playNote.Note + Transpose));

                        if (playNote.On)
                        {
                            _midiOut.Send(0x90 | (playNote.Ch & 0xF) | (finalNote << 8) | (scaledVelocity << 16));
                        }
                        else
                        {
                            _midiOut.Send(0x80 | (playNote.Ch & 0xF) | (finalNote << 8));
                        }
                    }
                    catch { }
                }

                if (Macro && playNote.On)
                {
                    char? macroKey = MapKey(playNote.Note, Transpose);
                    if (macroKey.HasValue)
                    {
                        Win32Input.PressKeyVk(macroKey.Value);
                    }
                }

                double progressTime = Math.Min(currentElapsed, Duration);
                if (Math.Abs(progressTime - lastReportedProgress) >= 0.1 || eventIndex == totalNotes - 1)
                {
                    if (OnProgress != null)
                    {
                        OnProgress(progressTime, Duration);
                    }
                    lastReportedProgress = progressTime;
                }

                eventIndex++;
            }

            Playing = false;
            Paused = false;
            Silence();

            if (OnFinish != null)
            {
                OnFinish();
            }
        }
    }
}
