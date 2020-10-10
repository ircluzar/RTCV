namespace RTCV.UI.Input
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using RTCV.NetCore;

    internal class Input
    {
        public static Input Instance { get; private set; }
        readonly Thread UpdateThread;
        private bool KillUpdateThread = false;

        private Input()
        {
            UpdateThread = new Thread(UpdateThreadProc)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal //why not? this thread shouldn't be very heavy duty, and we want it to be responsive
            };
            UpdateThread.Start();
        }

        public static void Initialize()
        {
            lock (UICore.InputLock)
            {
                Cleanup();
                GamePad.Initialize();
                KeyInput.Initialize();
                //IPCKeyInput.Initialize();
                GamePad360.Initialize();
                Instance = new Input();
            }
        }

        public static void Cleanup()
        {
            if (Instance?.UpdateThread?.IsAlive ?? false)
            {
                Instance.KillUpdateThread = true;
                Instance.UpdateThread.Join();
            }

            KeyInput.Cleanup();
            GamePad.Cleanup();
        }

        private readonly WorkingDictionary<string, object> ModifierState = new WorkingDictionary<string, object>();
        private readonly WorkingDictionary<string, bool> LastState = new WorkingDictionary<string, bool>();
        private readonly WorkingDictionary<string, bool> UnpressState = new WorkingDictionary<string, bool>();
        private readonly HashSet<string> IgnoreKeys = new HashSet<string>(new[] { "LeftShift", "RightShift", "LeftControl", "RightControl", "LeftAlt", "RightAlt" });
        private readonly WorkingDictionary<string, float> FloatValues = new WorkingDictionary<string, float>();
        private readonly WorkingDictionary<string, float> FloatDeltas = new WorkingDictionary<string, float>();
        private bool trackdeltas = false;

        void HandleButton(string button, bool newState)
        {
            bool isModifier = IgnoreKeys.Contains(button);
            if (LastState[button] && newState)
            {
                return;
            }

            if (!LastState[button] && !newState)
            {
                return;
            }

            //apply
            //NOTE: this is not quite right. if someone held leftshift+rightshift it would be broken. seems unlikely, though.
            if (button == "LeftShift")
            {
                _Modifiers &= ~ModifierKeys.Shift;
                if (newState)
                {
                    _Modifiers |= ModifierKeys.Shift;
                }
            }
            if (button == "RightShift") { _Modifiers &= ~ModifierKeys.Shift; if (newState)
                {
                    _Modifiers |= ModifierKeys.Shift;
                }
            }
            if (button == "LeftControl") { _Modifiers &= ~ModifierKeys.Control; if (newState)
                {
                    _Modifiers |= ModifierKeys.Control;
                }
            }
            if (button == "RightControl") { _Modifiers &= ~ModifierKeys.Control; if (newState)
                {
                    _Modifiers |= ModifierKeys.Control;
                }
            }
            if (button == "LeftAlt") { _Modifiers &= ~ModifierKeys.Alt; if (newState)
                {
                    _Modifiers |= ModifierKeys.Alt;
                }
            }
            if (button == "RightAlt") { _Modifiers &= ~ModifierKeys.Alt; if (newState)
                {
                    _Modifiers |= ModifierKeys.Alt;
                }
            }

            if (UnpressState.ContainsKey(button))
            {
                if (newState)
                {
                    return;
                }

                Console.WriteLine("Removing Unpress {0} with newState {1}", button, newState);
                UnpressState.Remove(button);
                LastState[button] = false;
                return;
            }

            //dont generate events for things like Ctrl+LeftControl
            ModifierKeys mods = _Modifiers;
            if (button == "LeftShift")
            {
                mods &= ~ModifierKeys.Shift;
            }

            if (button == "RightShift")
            {
                mods &= ~ModifierKeys.Shift;
            }

            if (button == "LeftControl")
            {
                mods &= ~ModifierKeys.Control;
            }

            if (button == "RightControl")
            {
                mods &= ~ModifierKeys.Control;
            }

            if (button == "LeftAlt")
            {
                mods &= ~ModifierKeys.Alt;
            }

            if (button == "RightAlt")
            {
                mods &= ~ModifierKeys.Alt;
            }

            var ie = new InputEvent
            {
                EventType = newState ? InputEventType.Press : InputEventType.Release,
                LogicalButton = new LogicalButton(button, mods)
            };
            LastState[button] = newState;

            //track the pressed events with modifiers that we send so that we can send corresponding unpresses with modifiers
            //this is an interesting idea, which we may need later, but not yet.
            //for example, you may see this series of events: press:ctrl+c, release:ctrl, release:c
            //but you might would rather have press:ctr+c, release:ctrl+c
            //this code relates the releases to the original presses.
            //UPDATE - this is necessary for the frame advance key, which has a special meaning when it gets stuck down
            //so, i am adding it as of 11-sep-2011
            if (newState)
            {
                ModifierState[button] = ie.LogicalButton;
            }
            else
            {
                if (ModifierState[button] != null)
                {
                    LogicalButton alreadyReleased = ie.LogicalButton;
                    var ieModified = new InputEvent
                    {
                        LogicalButton = (LogicalButton)ModifierState[button],
                        EventType = InputEventType.Release
                    };
                    if (ieModified.LogicalButton != alreadyReleased)
                    {
                        _NewEvents.Add(ieModified);
                    }
                }
                ModifierState[button] = null;
            }

            _NewEvents.Add(ie);
        }

        ModifierKeys _Modifiers;
        private readonly List<InputEvent> _NewEvents = new List<InputEvent>();

        //do we need this?
        public void ClearEvents()
        {
            lock (this)
            {
                InputEvents.Clear();
            }
        }

        private readonly Queue<InputEvent> InputEvents = new Queue<InputEvent>();

        public InputEvent DequeueEvent()
        {
            lock (this)
            {
                if (InputEvents.Count == 0)
                {
                    return null;
                }
                else
                {
                    return InputEvents.Dequeue();
                }
            }
        }

        void EnqueueEvent(InputEvent ie)
        {
            lock (this)
            {
                InputEvents.Enqueue(ie);
            }
        }

        public List<Tuple<string, float>> GetFloats()
        {
            List<Tuple<string, float>> FloatValuesCopy = new List<Tuple<string, float>>();
            lock (FloatValues)
            {
                foreach (var kvp in FloatValues)
                {
                    FloatValuesCopy.Add(new Tuple<string, float>(kvp.Key, kvp.Value));
                }
            }
            return FloatValuesCopy;
        }

        void UpdateThreadProc()
        {
            for (; ; )
            {
                if (KillUpdateThread)
                {
                    return;
                }

                var keyEvents = KeyInput.Update();
                GamePad.UpdateAll();
                GamePad360.UpdateAll();

                //this block is going to massively modify data structures that the binding method uses, so we have to lock it all
                lock (this)
                {
                    _NewEvents.Clear();

                    //analyze keys
                    foreach (var ke in keyEvents)
                    {
                        HandleButton(ke.Key.ToString(), ke.Pressed);
                    }

                    lock (FloatValues)
                    {
                        //FloatValues.Clear();

                        //analyze xinput
                        foreach (var pad in GamePad360.EnumerateDevices())
                        {
                            string xname = "X" + pad.PlayerNumber + " ";
                            for (int b = 0; b < pad.NumButtons; b++)
                            {
                                HandleButton(xname + pad.ButtonName(b), pad.Pressed(b));
                            }

                            foreach (var sv in pad.GetFloats())
                            {
                                string n = xname + sv.Item1;
                                float f = sv.Item2;
                                if (trackdeltas)
                                {
                                    FloatDeltas[n] += Math.Abs(f - FloatValues[n]);
                                }

                                FloatValues[n] = f;
                            }
                        }

                        //analyze joysticks
                        foreach (var pad in GamePad.EnumerateDevices())
                        {
                            string jname = "J" + pad.PlayerNumber + " ";
                            for (int b = 0; b < pad.NumButtons; b++)
                            {
                                HandleButton(jname + pad.ButtonName(b), pad.Pressed(b));
                            }

                            foreach (var sv in pad.GetFloats())
                            {
                                string n = jname + sv.Item1;
                                float f = sv.Item2;
                                //if (n == "J5 RotationZ")
                                //    System.Diagnostics.Debugger.Break();
                                if (trackdeltas)
                                {
                                    FloatDeltas[n] += Math.Abs(f - FloatValues[n]);
                                }

                                FloatValues[n] = f;
                            }
                        }
                    }

                    bool allowInput = ((bool?)AllSpec.UISpec?[NetCore.Commands.Basic.RTCInFocus] ?? true) || ((bool?)AllSpec.VanguardSpec?[NetCore.Commands.Emulator.InFocus] ?? true);

                    bool swallow = !allowInput;

                    foreach (var ie in _NewEvents)
                    {
                        //events are swallowed in some cases:
                        if (ie.LogicalButton.Alt && !allowInput)
                        {
                        }
                        else if (ie.EventType == InputEventType.Press && swallow)
                        {
                        }
                        else
                        {
                            EnqueueEvent(ie);
                        }
                    }
                } //lock(this)

                //arbitrary selection of polling frequency:
                Thread.Sleep(10);
            }
        }

        public void StartListeningForFloatEvents()
        {
            lock (FloatValues)
            {
                FloatDeltas.Clear();
                trackdeltas = true;
            }
        }

        public string GetNextFloatEvent()
        {
            lock (FloatValues)
            {
                foreach (var kvp in FloatDeltas)
                {
                    // need to wiggle the stick a bit
                    if (kvp.Value >= 20000.0f)
                    {
                        return kvp.Key;
                    }
                }
            }
            return null;
        }

        public void StopListeningForFloatEvents()
        {
            lock (FloatValues)
            {
                trackdeltas = false;
            }
        }

        public static void Update()
        {
            //TODO - for some reason, we may want to control when the next event processing step happens
            //so i will leave this method here for now..
        }

        //returns the next Press event, if available. should be useful
        public string GetNextBindEvent()
        {
            //this whole process is intimately involved with the data structures, which can conflict with the input thread.
            lock (this)
            {
                if (InputEvents.Count == 0)
                {
                    return null;
                }

                if (!(bool?)AllSpec.UISpec[NetCore.Commands.Basic.RTCInFocus] ?? true)
                {
                    return null;
                }

                //we only listen to releases for input binding, because we need to distinguish releases of pure modifierkeys from modified keys
                //if you just pressed ctrl, wanting to bind ctrl, we'd see: pressed:ctrl, unpressed:ctrl
                //if you just pressed ctrl+c, wanting to bind ctrl+c, we'd see: pressed:ctrl, pressed:ctrl+c, unpressed:ctrl+c, unpressed:ctrl
                //so its the first unpress we need to listen for

                while (InputEvents.Count != 0)
                {
                    var ie = DequeueEvent();

                    //as a special perk, we'll accept escape immediately
                    if (ie.EventType == InputEventType.Press && ie.LogicalButton.Button == "Escape")
                    {
                        goto ACCEPT;
                    }

                    if (ie.EventType == InputEventType.Press)
                    {
                        continue;
                    }

ACCEPT:
                    Console.WriteLine("Bind Event: {0} ", ie);

                    foreach (var kvp in LastState)
                    {
                        if (kvp.Value)
                        {
                            Console.WriteLine("Unpressing " + kvp.Key);
                            UnpressState[kvp.Key] = true;
                        }
                    }

                    InputEvents.Clear();

                    return ie.LogicalButton.ToString();
                }

                return null;
            }
        }

        //sets a key as unpressed for the binding system
        public void BindUnpress(System.Windows.Forms.Keys key)
        {
            //only validated for Return
            string keystr = key.ToString();
            UnpressState[keystr] = true;
            LastState[keystr] = true;
        }
    }
}
