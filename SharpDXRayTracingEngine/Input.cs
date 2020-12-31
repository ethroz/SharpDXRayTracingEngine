﻿using SharpDX;
using SharpDX.DirectInput;

namespace SharpDXRayTracingEngine
{
    public class Input
    {
        // Input Fields
        private Engine Reference;
        public Mouse mouse;
        public Button[] buttons;
        private Vector2 DeltaMousePos;
        public Keyboard keyboard;
        public Chey[] cheyArray;

        public int RefreshRate = 1000;
        public double elapsedTime;
        private long t1, t2;
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        public Input(Engine reference)
        {
            Reference = reference;
        }

        public void InitializeMouse()
        {
            mouse = new Mouse(new DirectInput());
            mouse.Acquire();
            var state = mouse.GetCurrentState();
            var allButtons = state.Buttons;
            buttons = new Button[allButtons.Length];
            for (int i = 0; i < allButtons.Length; i++)
                buttons[i] = new Button();
            DeltaMousePos = new Vector2(state.X, state.Y);
        }

        public void InitializeKeyboard()
        {
            keyboard = new Keyboard(new DirectInput());
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
            var state = keyboard.GetCurrentState();
            var allKeys = state.AllKeys;
            cheyArray = new Chey[allKeys.Count];
            for (int i = 0; i < allKeys.Count; i++)
                cheyArray[i] = new Chey(allKeys[i]);
        }

        public void GetMouseData()
        {
            mouse.Poll();
            var state = mouse.GetCurrentState();
            var butts = state.Buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool pressed = butts[i];
                buttons[i].Down = buttons[i].Raised && pressed;
                buttons[i].Up = buttons[i].Held && !pressed;
                buttons[i].Held = pressed;
                buttons[i].Raised = !pressed;
            }
            DeltaMousePos += new Vector2(state.X, state.Y);
        }

        public void GetKeys()
        {
            keyboard.Poll();
            var state = keyboard.GetCurrentState();
            for (int i = 0; i < cheyArray.Length; i++)
            {
                bool pressed = state.IsPressed(cheyArray[i].key);
                cheyArray[i].Down = cheyArray[i].Raised && pressed;
                cheyArray[i].Up = cheyArray[i].Held && !pressed;
                cheyArray[i].Held = pressed;
                cheyArray[i].Raised = !pressed;
            }
        }

        private void GetTime()
        {
            t2 = sw.ElapsedTicks;
            elapsedTime = (t2 - t1) / 10000000.0;
            if (RefreshRate != 0)
            {
                while (1.0 / elapsedTime > RefreshRate)
                {
                    t2 = sw.ElapsedTicks;
                    elapsedTime = (t2 - t1) / 10000000.0;
                }
            }
            t1 = t2;
            //Engine.print("Updates per Second: " + (1.0 / (elapsedTime)).ToString("G4"));
        }

        public void ControlLoop()
        {
            sw.Start();
            t1 = sw.ElapsedTicks;
            while (true)
            {
                GetTime();
                Reference.UserInput();
                GetMouseData();
                GetKeys();
            }
        }

        public bool KeyDown(Key key)
        {
            return FindChey(key).Down;
        }

        public bool KeyUp(Key key)
        {
            return FindChey(key).Up;
        }

        public bool KeyHeld(Key key)
        {
            return FindChey(key).Held;
        }

        public bool KeyRaised(Key key)
        {
            return FindChey(key).Raised;
        }

        public Chey FindChey(Key key)
        {
            for (int i = 0; i < cheyArray.Length; i++)
            {
                if (cheyArray[i].key == key)
                    return cheyArray[i];
            }
            return null;
        }

        public bool ButtonDown(int button)
        {
            return buttons[button].Down;
        }

        public bool ButtonUp(int button)
        {
            return buttons[button].Up;
        }

        public bool ButtonHeld(int button)
        {
            return buttons[button].Held;
        }

        public bool ButtonRaised(int button)
        {
            return buttons[button].Raised;
        }

        public Vector2 GetDeltaMousePos()
        {
            Vector2 pos = DeltaMousePos;
            DeltaMousePos = new Vector2();
            return pos;
        }
    }
}