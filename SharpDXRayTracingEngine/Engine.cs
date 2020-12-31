using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Windows;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using System.Threading.Tasks;

namespace SharpDXRayTracingEngine
{
    public class Engine : IDisposable
    {
        // Graphics Fields
        private RenderForm renderForm;
        private D3D11.Device device;
        private DeviceContext deviceContext;
        private SwapChain swapChain;
        private RenderTargetView renderTargetView;
        private Vector3[] planeVertices = new Vector3[]
        {
            new Vector3(-3.0f, -1.0f, 0.0f),
            new Vector3(0.0f, 2.0f, 0.0f),
            new Vector3(3.0f, -1.0f, 0.0f)
        };
        private D3D11.Buffer planeVertexBuffer;
        private D3D11.BufferDescription constantBD;
        private D3D11.Buffer buffer;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private InputElement[] inputElements = new InputElement[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0)
        };
        private ShaderSignature inputSignature;
        private InputLayout inputLayout;
        private Viewport viewport;
        private MainBuffer mainBuffer;
        private EmptyBuffer emptyData;
        private BufferDescription vertexBD;
        private D3D11.Buffer vertexBuffer;
        private int NumberOfTriangles;
        private string path = @"C:\Users\ethro\source\repos\SharpDXRayTracingEngine\SharpDXRayTracingEngine\";
        public static float Deg2Rad = (float)Math.PI / 180.0f;

        // Controllable Graphics Settings
        public int RefreshRate = 0; // set to 0 for uncapped
        public int Width = 2560, Height = 1440; // DO NOT exceed display resolution
        public bool Running = true;
        public enum WindowState { Normal, Minimized, Maximized, FullScreen };
        private WindowState State = WindowState.FullScreen;
        private int WindowStateIncrement = 0;

        // Frame Fields
        public double frameTime;
        private long t1, t2;
        private System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        // Game Fields
        public Input Input;
        private Vector3 BGCol = new Vector3();//new Vector3(0.5f, 0.8f, 1.0f);
        private float MinBrightness = 0.0f;//0.25f;
        private int RayCount = 1; // will be squared
        private int RayDepth = 1; // ####################################################################################################################### <- its here, retard
        private List<Gameobject> gameobjects = new List<Gameobject>();
        private List<Sphere> spheres = new List<Sphere>();
        private List<Light> lights = new List<Light>();
        private Vector3 EyePos = new Vector3();
        private Vector2 EyeRot = new Vector2();
        private float MoveSpeed = 4.0f;
        private float Sensitivity = 0.04f;

        private bool Test()
        {
            print("Done");
            return false;
        }

        public Engine()
        {
            Input = new Input(this);
            OnAwake();
            if (Test())
            {
                Console.ReadKey();
                Environment.Exit(0);
            }

            renderForm = new RenderForm("SharpDXRayTracingEngine")
            {
                ClientSize = new Size(Width, Height),
                AllowUserResizing = true
            };
            if (State == WindowState.FullScreen)
            {
                renderForm.TopMost = true;
                renderForm.FormBorderStyle = FormBorderStyle.None;
                renderForm.WindowState = FormWindowState.Maximized;
            }
            else if (State == WindowState.Maximized)
            {
                renderForm.WindowState = FormWindowState.Maximized;
            }
            else if (State == WindowState.Minimized)
            {
                renderForm.TopMost = false;
                renderForm.FormBorderStyle = FormBorderStyle.Sizable;
                renderForm.WindowState = FormWindowState.Minimized;
            }

            Input.InitializeMouse();
            Input.InitializeKeyboard();
            InitializeDeviceResources();
            InitializePlane();
            InitializeShaders();
            OnStart();

            t1 = sw.ElapsedTicks;
        }

        public void Run()
        {
            void a() => Input.ControlLoop();
            //Task t2 = new Task(() => );
            Task.Run(a);
            RenderLoop.Run(renderForm, RenderCallBack);
        }

        private void RenderCallBack()
        {
            if (WindowStateIncrement != 0)
                CycleWindowState();
            GetTime();
            if (!Running)
                return;
            OnUpdate();
            Draw();
        }

        private void InitializeDeviceResources()
        {
            SwapChainDescription swapChainDesc = new SwapChainDescription()
            {
                ModeDescription = new ModeDescription(Width, Height, new Rational(10000, 1), Format.B8G8R8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = renderForm.Handle,
                IsWindowed = true
            };
            D3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, swapChainDesc, out device, out swapChain);
            deviceContext = device.ImmediateContext;
            using (Texture2D backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                renderTargetView = new RenderTargetView(device, backBuffer);
            }
            viewport = new Viewport(0, 0, Width, Height);
            deviceContext.Rasterizer.SetViewport(viewport);
        }

        private void InitializePlane()
        {
            planeVertexBuffer = D3D11.Buffer.Create(device, BindFlags.VertexBuffer, planeVertices);
        }

        private void InitializeShaders()
        {
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile(path + "Shaders.hlsl", "vertexShader", "vs_5_0", ShaderFlags.Debug))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new VertexShader(device, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile(path + "Shaders.hlsl", "pixelShader", "ps_5_0", ShaderFlags.Debug))
            {
                pixelShader = new PixelShader(device, pixelShaderByteCode);
            }
            deviceContext.VertexShader.Set(vertexShader);
            deviceContext.PixelShader.Set(pixelShader);

            inputLayout = new InputLayout(device, inputSignature, inputElements);
            deviceContext.InputAssembler.InputLayout = inputLayout;

            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }

        private void SetShaderResources()
        {
            constantBD = new BufferDescription(AssignSize<MainBuffer>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
            mainBuffer = new MainBuffer();
            buffer = D3D11.Buffer.Create(device, ref mainBuffer, constantBD);
            deviceContext.PixelShader.SetConstantBuffer(0, buffer);

            vertexBD = new BufferDescription(emptyData.size * 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, 0, 0);
            vertexBuffer = D3D11.Buffer.Create(device, ref emptyData, vertexBD);
            deviceContext.PixelShader.SetConstantBuffer(1, vertexBuffer);
        }

        private unsafe void UpdateShaderResources()
        {
            //buffer data
            Matrix3x3 rotx = Matrix3x3.RotationX(EyeRot.X * Deg2Rad);
            Matrix3x3 roty = Matrix3x3.RotationY(EyeRot.Y * Deg2Rad);
            Matrix3x3 rot = rotx * roty;
            mainBuffer = new MainBuffer(rot, EyePos, BGCol, Width, Height, MinBrightness, (sw.ElapsedTicks / 10000000.0f) % 60.0f, RayCount, RayDepth, NumberOfTriangles, spheres.Count, lights.Count);
            deviceContext.UpdateSubresource(ref mainBuffer, buffer, 0);

            //vertex data
            Vector4[] data = new Vector4[emptyData.size];
            int counter = 0;
            for (int i = 0; i < gameobjects.Count; i++)
            {
                for (int j = 0; j < gameobjects[i].triangles.Length; j++)
                {
                    data[counter * 3] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[0], 0.0f);
                    data[counter * 3 + 1] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[1], 0.0f);
                    data[counter * 3 + 2] = new Vector4(gameobjects[i].projectedTriangles[j].Vertices[2], 0.0f);
                    data[NumberOfTriangles * 3 + counter * 3] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[0], gameobjects[i].projectedTriangles[j].Specials[0]);
                    data[NumberOfTriangles * 3 + counter * 3 + 1] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[1], gameobjects[i].projectedTriangles[j].Specials[1]);
                    data[NumberOfTriangles * 3 + counter * 3 + 2] = new Vector4(gameobjects[i].projectedTriangles[j].Normals[2], gameobjects[i].projectedTriangles[j].Specials[2]);
                    data[NumberOfTriangles * 6 + counter] = gameobjects[i].projectedTriangles[j].Color;
                    counter++;
                }
            }
            for (int i = 0; i < spheres.Count; i++)
            {
                data[(NumberOfTriangles * 7) + (i * 3)] = new Vector4(spheres[i].position, spheres[i].radius);
                data[(NumberOfTriangles * 7) + (i * 3) + 1] = spheres[i].color;
                data[(NumberOfTriangles * 7) + (i * 3) + 2] = spheres[i].Data;
            }
            for (int i = 0; i < lights.Count; i++)
            {
                data[(NumberOfTriangles * 7) + (spheres.Count * 3) + (i * 3)] = lights[i].Data0;
                data[(NumberOfTriangles * 7) + (spheres.Count * 3) + (i * 3) + 1] = lights[i].Data1;
                data[(NumberOfTriangles * 7) + (spheres.Count * 3) + (i * 3) + 2] = lights[i].Data2;
            }
            fixed (Vector4* fp = &data[0])
                deviceContext.UpdateSubresourceSafe(new DataBox((IntPtr)fp), vertexBuffer, 16, 0);
        }

        private int AssignSize<T>() where T : struct
        {
            int size = Utilities.SizeOf<T>();
            return size + (16 - (size % 16));
        }

        private void GetTime()
        {
            t2 = sw.ElapsedTicks;
            frameTime = (t2 - t1) / 10000000.0;
            if (RefreshRate != 0)
            {
                while (1.0 / frameTime > RefreshRate)
                {
                    t2 = sw.ElapsedTicks;
                    frameTime = (t2 - t1) / 10000000.0;
                }
            }
            t1 = t2;
            renderForm.Text = "SharpDXCPURayTracingEngine   FPS:" + (1.0 / (frameTime)).ToString("G4");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool boolean)
        {
            Input.mouse.Dispose();
            Input.keyboard.Dispose();
            device.Dispose();
            deviceContext.Dispose();
            swapChain.Dispose();
            renderTargetView.Dispose();
            planeVertexBuffer.Dispose();
            buffer.Dispose();
            vertexBuffer.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();
            inputLayout.Dispose();
            inputSignature.Dispose();
            renderForm.Dispose();
        }

        /////////////////////////////////////

        private void Draw()
        {
            UpdateShaderResources();

            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(planeVertexBuffer, Utilities.SizeOf<Vector3>(), 0));
            deviceContext.Draw(planeVertices.Length, 0);

            try
            {
                swapChain.Present(0, PresentFlags.None);
            }
            catch (SharpDXException s)
            {
                print(s.Message);
            }
        }

        public virtual void OnAwake()
        {
            Cursor.Hide();
            sw.Start();
            lights.Add(new Light(new Vector3(-50.0f, 100.0f, -100.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), 5.0f, 20000.0f));
            gameobjects.AddRange(Gameobject.GetObjectsFromFile(path + @"Objects\Objects.obj"));
            gameobjects[0].ChangeGameObjectColor(new Vector4(1.0f, 0.0f, 0.0f, 0.3f));
            gameobjects[1].ChangeGameObjectColor(new Vector4(0.0f, 0.0f, 1.0f, 0.7f));
            gameobjects[2].ChangeGameObjectColor(new Vector4(0.0f, 1.0f, 0.0f, 0.3f));
            gameobjects[3].ChangeGameObjectColor(new Vector4(1.0f, 0.0f, 1.0f, 0.3f));
            gameobjects[4].ChangeGameObjectColor(new Vector4(1.0f, 1.0f, 0.0f, 0.3f));
            gameobjects[1].ChangeGameObjectFlatness(true);
            gameobjects[2].ChangeGameObjectFlatness(true);
            gameobjects[3].ChangeGameObjectFlatness(true);
            gameobjects[4].ChangeGameObjectFlatness(true);
            TriNormsCol[] planeVerts = new TriNormsCol[2]
            {
                new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3(-100.0f, 0.0f, 100.0f), new Vector3( 100.0f, 0.0f,  100.0f) }, 
                new Vector3(0.0f, 1.0f, 0.0f), new Vector4(1.0f, 1.0f, 1.0f, 0.0f)),
                new TriNormsCol(new Vector3[] { new Vector3(-100.0f, 0.0f, -100.0f), new Vector3( 100.0f, 0.0f, 100.0f), new Vector3( 100.0f, 0.0f, -100.0f) }, 
                new Vector3(0.0f, 1.0f, 0.0f), new Vector4(1.0f, 1.0f, 1.0f, 0.0f))
            };
            gameobjects.Add(new Gameobject("plane", new Vector3(0.0f, -1.0f, 0.0f), new Vector3(), new Vector3(1.0f), planeVerts));
            spheres.Add(new Sphere(new Vector3(0.0f, 0.0f, 3.0f), 1.0f, new Vector4(0.0f, 0.0f, 0.0f, 1.0f), new Vector4(1.5f, 0.0f, 0.0f, 0.0f)));
            UpdateShaderPolyCount();
        }

        public virtual void OnStart()
        {
            SetShaderResources();
        }

        public virtual void OnUpdate()
        {
            for (int i = 0; i < gameobjects.Count; i++)
            {
                gameobjects[i].projectedTriangles = new TriNormsCol[gameobjects[i].triangles.Length];
                for (int j = 0; j < gameobjects[i].triangles.Length; j++)
                {
                    gameobjects[i].projectedTriangles[j].Vertices = new Vector3[3];
                    gameobjects[i].projectedTriangles[j].Normals = new Vector3[3];
                    gameobjects[i].projectedTriangles[j].Specials = new float[3];
                    for (int k = 0; k < 3; k++)
                    {
                        gameobjects[i].projectedTriangles[j].Vertices[k] = gameobjects[i].triangles[j].Vertices[k] + gameobjects[i].position;
                        gameobjects[i].projectedTriangles[j].Normals[k] = gameobjects[i].triangles[j].Normals[k];
                        gameobjects[i].projectedTriangles[j].Specials[k] = gameobjects[i].triangles[j].Specials[k];
                    }
                    gameobjects[i].projectedTriangles[j].Color = gameobjects[i].triangles[j].Color;
                }
            }
        }

        public virtual void UserInput()
        {
            if (Input.KeyDown(Key.Tab))
                IncrementWindowState();
            if (Input.KeyDown(Key.Escape))
                Environment.Exit(0);
            if (Input.KeyDown(Key.P))
                Running = !Running;
            if (!Running)
                return;

            if (Input.KeyHeld(Key.LeftShift))
                MoveSpeed *= 2.0f;
            Vector2 pos = Input.GetDeltaMousePos();
            EyeRot.Y += pos.X * Sensitivity;
            EyeRot.X += pos.Y * Sensitivity;
            EyeRot.X = Math.Max(Math.Min(EyeRot.X, 90.0f), -90.0f);
            Matrix rotx = Matrix.RotationX(EyeRot.X * Deg2Rad);
            Matrix roty = Matrix.RotationY(EyeRot.Y * Deg2Rad);
            Matrix rot = rotx * roty;
            float normalizer = Math.Max((float)Math.Sqrt((Input.KeyHeld(Key.A) ^ Input.KeyHeld(Key.D) ? 1 : 0) + (Input.KeyHeld(Key.W) ^ Input.KeyHeld(Key.S) ? 1 : 0) + (Input.KeyHeld(Key.E) ^ Input.KeyHeld(Key.Q) ? 1 : 0)), 1.0f);
            Vector3 forward = Vector3.TransformNormal(Vector3.ForwardLH, rot) / normalizer;
            Vector3 right = Vector3.TransformNormal(Vector3.Right, rot) / normalizer;
            Vector3 up = Vector3.TransformNormal(Vector3.Up, rot) / normalizer;
            if (Input.KeyHeld(Key.A))
                EyePos -= right * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.D))
                EyePos += right * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.W))
                EyePos += forward * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.S))
                EyePos -= forward * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.Q))
                EyePos -= up * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.E))
                EyePos += up * (float)Input.elapsedTime * MoveSpeed;
            if (Input.KeyHeld(Key.LeftShift))
                MoveSpeed /= 2.0f;

            if (Input.KeyHeld(Key.F))
                spheres[0].position.X -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.H))
                spheres[0].position.X += MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.T))
                spheres[0].position.Z += MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.G))
                spheres[0].position.Z -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.R))
                spheres[0].position.Y -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.Y))
                spheres[0].position.Y += MoveSpeed * (float)Input.elapsedTime;

            if (Input.KeyHeld(Key.J))
                lights[0].Data0.X -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.L))
                lights[0].Data0.X += MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.I))
                lights[0].Data0.Z += MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.K))
                lights[0].Data0.Z -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.U))
                lights[0].Data0.Y -= MoveSpeed * (float)Input.elapsedTime;
            if (Input.KeyHeld(Key.O))
                lights[0].Data0.Y += MoveSpeed * (float)Input.elapsedTime;
        }

        /////////////////////////////////////

        private void UpdateShaderPolyCount()
        {
            NumberOfTriangles = 0;
            for (int i = 0; i < gameobjects.Count; i++)
                NumberOfTriangles += gameobjects[i].triangles.Length;
            emptyData = new EmptyBuffer(NumberOfTriangles * 7 + spheres.Count * 3 + lights.Count * 3);
            string[] shader = File.ReadAllLines(path + "Shaders.hlsl");
            int index0 = -1, index1 = -1, index2 = -1;
            for (int i = 0; i < shader.Length; i++)
            {
                if (shader[i].Contains("float3 Vertices["))
                    index0 = i;
                if (shader[i].Contains("float distances["))
                {
                    if (index1 == -1)
                        index1 = i;
                    else
                        index2 = i;
                }
            }
            shader[index0] = "    float3 Vertices[" + (NumberOfTriangles * 3) + "];";
            shader[index0 + 1] = "    float4 Normals[" + (NumberOfTriangles * 3) + "];";
            shader[index0 + 2] = "    float4 Colors[" + NumberOfTriangles + "];";
            shader[index0 + 3] = "    float4 Spheres[" + (spheres.Count * 3) + "];";
            shader[index0 + 4] = "    float4 Lights[" + (lights.Count * 3) + "];";
            shader[index1] = "    float distances[" + (NumberOfTriangles + spheres.Count + lights.Count) + "];";
            shader[index2] = "    float distances[" + (NumberOfTriangles + spheres.Count + lights.Count) + "];";
            shader[index2 + 1] = "    Ray rays[" + (RayDepth + 1) + "];";
            shader[index2 + 2] = "	float dropOff[" + (RayDepth + 1) + "];";
            File.WriteAllLines(path + "Shaders.hlsl", shader);
        }

        public void IncrementWindowState()
        {
            WindowStateIncrement++;
        }

        private void CycleWindowState()
        {
            for (int i = 0; i < WindowStateIncrement; i++)
            {
                switch (State)
                {
                    case WindowState.Minimized:
                        State = WindowState.Normal;
                        renderForm.TopMost = false;
                        renderForm.FormBorderStyle = FormBorderStyle.Sizable;
                        renderForm.WindowState = FormWindowState.Normal;
                        break;
                    case WindowState.Normal:
                        State = WindowState.Maximized;
                        renderForm.TopMost = false;
                        renderForm.FormBorderStyle = FormBorderStyle.Sizable;
                        renderForm.WindowState = FormWindowState.Maximized;
                        break;
                    case WindowState.Maximized:
                        State = WindowState.FullScreen;
                        renderForm.TopMost = true;
                        renderForm.FormBorderStyle = FormBorderStyle.None;
                        renderForm.WindowState = FormWindowState.Normal;
                        renderForm.WindowState = FormWindowState.Maximized;
                        break;
                    case WindowState.FullScreen:
                        State = WindowState.Minimized;
                        renderForm.TopMost = false;
                        renderForm.FormBorderStyle = FormBorderStyle.Sizable;
                        renderForm.WindowState = FormWindowState.Minimized;
                        break;
                }
            }
            WindowStateIncrement = 0;
        }

        public static void print(object message)
        {
            Console.WriteLine(message);
        }

        public static void print()
        {
            Console.WriteLine();
        }
    }
}