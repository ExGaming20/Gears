using System.Diagnostics;
using System.IO;
using Gears.Diagnostics;
using Gears.Graphics;
using Gears.Utilities;
using Gears.World;
using Gears.World.Components;
using Gears.World.Components.AddonComponents;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static System.Formats.Asn1.AsnWriter;

namespace Gears
{
    public class Game : GameWindow
    {
        public static Game Instance { get; private set; }

        public static Vector2 WindowSize;

        public float FixedUpdateTimer;
        public float FixedUpdateDataTimer;

        public static List<Material> Materials = new List<Material>();
        public static List<Mesh> Meshes = new List<Mesh>();
        public static List<Texture> Textures = new List<Texture>();
        public static List<Shader> Shaders = new List<Shader>();

        public static readonly Dictionary<int, Texture> TextureByUUID = new();
        public static readonly Dictionary<int, Material> MaterialByUUID = new();
        private readonly Dictionary<int, Shader> _shaderByUUID = new();

        private readonly Dictionary<SubMesh, DrawMesh> _meshCache = new();

        // Scenes are owned by SceneManager now; this is just a convenience accessor so the rest
        // of Game.cs reads the same as before. Never cache the result across frames — the active
        // scene can change (SceneManager.LoadScene) between one call site and the next.
        private static Scene? ActiveScene => SceneManager.ActiveScene;

        public const string MainSceneName = "MainScene";

        public static List<RenderRequest> RenderQueue = new List<RenderRequest>();

        // Separate queue used only while an EProbe is capturing (see BeginProbeCapture / RenderProbeFace).
        // Keeping it distinct from RenderQueue means probe baking never depends on where the main
        // per-frame queue happens to be in its populate/flush/clear cycle.
        private static readonly List<RenderRequest> ProbeCaptureQueue = new List<RenderRequest>();
        private static bool _capturingForProbe = false;

        public static string ActiveCameraName = "MainCamera";

        private FrameBuffer _frameBuffer;
        private ScreenQuad _screenQuad;
        private Shader _screenShader;

        private ForwardPlusRenderer _forwardPlus;
        private Shader _depthPrepassShader;
        private Shader _lightCullingShader;

        private ShadowMapRenderer _shadowMapRenderer;
        private Shader _shadowDepthShader;
        private Shader _shadowCubeShader;

        public static Texture MissingTexture;
        public static Shader MissingShader;
        public static Material MissingMaterial;
        public static Mesh MissingMesh;

        public static string WindowTitle;
        private static string OriginalTitle;

        public static Process currentProcess;

        public static float DeltaTime { get; private set; }

        public Game(int width, int height, string title) : base(GameWindowSettings.Default, new NativeWindowSettings() { Size = (width, height), Title = title, APIVersion = new Version(4, 3), Profile = ContextProfile.Core })
        {
            Instance = this;
            Input.CurrentWindow = this;

            WindowTitle = title;
            OriginalTitle = title;
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        protected override void OnLoad()
        {
            base.OnLoad();

            currentProcess = Process.GetCurrentProcess();

            Logger.Instance.ClearLogs();

            GL.ClearColor(0.3f, 0.6f, 0.9f, 1.0f);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Register scenes with the manager before loading any of them. Only one scene exists
            // right now, but this is the hook point for registering additional scenes (menus,
            // other levels) later.
            SceneManager.Register(MainSceneName, () => new Scene(MainSceneName));
            SceneManager.LoadScene(MainSceneName); // calls Scene.OnLoad() and sets it active

            Scene activeScene = ActiveScene!; // guaranteed non-null immediately after LoadScene

            Scene.SkyBox = new Texture("SkyBox", "CubeMap.png", TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge);

            MissingShader = AssetLoader.ReturnShaderMissing("Missing", "Missing");
            MissingTexture = AssetLoader.ReturnTextureMissing("MissingTexture", Path.Combine("Missing.png"));
            MissingMaterial = AssetLoader.ReturnMaterialMissing("Missing", Path.Combine("Fallback Assets", "Materials", "Missing"));
            MissingMesh = AssetLoader.ReturnMeshMissing("Missing", "Missing");

            AssetLoader.LoadAllAssets();

            foreach (var shader in Shaders) _shaderByUUID[shader.UUID] = shader;
            foreach (var texture in Textures) TextureByUUID[texture.UUID] = texture;
            foreach (var material in Materials) MaterialByUUID[material.UUID] = material;

            _frameBuffer = new FrameBuffer(new Vector2i(Size.X, Size.Y));
            _screenQuad = new ScreenQuad();
            _screenShader = new Shader(new ShaderProgram("ScreenVertex", "ScreenFragment"), "ScreenShader");

            _depthPrepassShader = new Shader(new ShaderProgram(vertexPath: "DepthPrepassVertex"), "DepthPrepassShader");
            _lightCullingShader = new Shader(new ShaderProgram(computePath: "LightCulling"), "LightCullingShader");
            _forwardPlus = new ForwardPlusRenderer(new Vector2i(Size.X, Size.Y), _depthPrepassShader, _lightCullingShader);

            // Directional/spot shadows reuse the same position-only shader as the Forward+ depth
            // pre-pass — depth-only, no fragment stage needed. Point light shadows need a real
            // fragment shader since they write linear distance-to-light, not raw depth.
            _shadowDepthShader = new Shader(new ShaderProgram(vertexPath: "DepthPrepassVertex"), "ShadowDepthShader");
            _shadowCubeShader = new Shader(new ShaderProgram(vertexPath: "ShadowCubeVertex", fragmentPath: "ShadowCubeFragment"), "ShadowCubeShader");
            _shadowMapRenderer = new ShadowMapRenderer(_shadowDepthShader, _shadowCubeShader);

            LightManager.RebuildStaticData();

            // test game objects

            var cameraObj = new GameObject(ActiveCameraName);
            activeScene.MakeGameObject(cameraObj);
            cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<BasicCameraFlyScript>();

            var cubeObj = new GameObject("cube");
            activeScene.MakeGameObject(cubeObj);
            cubeObj.AddComponent<MeshRenderer>();
            cubeObj.AddComponent<MeshFilter>();
            cubeObj.AddComponent<Spin>();

            var cubeObj2 = new GameObject("cube2");
            activeScene.MakeGameObject(cubeObj2);
            cubeObj2.AddComponent<MeshRenderer>();
            cubeObj2.AddComponent<MeshFilter>();
            cubeObj2.AddComponent<Spin>();

            var cubeObj3 = new GameObject("cube3");
            activeScene.MakeGameObject(cubeObj3);
            cubeObj3.AddComponent<MeshRenderer>();
            cubeObj3.AddComponent<MeshFilter>();
            cubeObj3.AddComponent<Spin>();

            var lightObj1 = new GameObject("light");
            activeScene.MakeGameObject(lightObj1);
            lightObj1.AddComponent<Light>();

            var lightObj2 = new GameObject("light2");
            activeScene.MakeGameObject(lightObj2);
            lightObj2.AddComponent<Light>();

            var UltraCube_mesh = FindLoadedAssets.FindMesh("UltraCube");
            var Plane_mesh = FindLoadedAssets.FindMesh("Plane");
            var Cube_mesh = FindLoadedAssets.FindMesh("Cube");

            if (UltraCube_mesh != null)
            {
                cubeObj.GetComponent<MeshFilter>().Mesh = UltraCube_mesh;
                cubeObj2.GetComponent<MeshFilter>().Mesh = UltraCube_mesh;
                cubeObj3.GetComponent<MeshFilter>().Mesh = UltraCube_mesh;
            }

            cubeObj3.GetComponent<Spin>().CCW = true;
            cubeObj3.GetComponent<Spin>().Speed = 120;
            cubeObj2.GetComponent<Spin>().Speed = 30;

            cubeObj.GetComponent<Transform>().Position = new Vector3(0, 0, -5);
            cubeObj2.GetComponent<Transform>().Position = new Vector3(0, 0, -10);
            cubeObj2.GetComponent<Transform>().Scale = Vector3.One / 2f;
            cubeObj3.GetComponent<Transform>().LocalScale = Vector3.One / 2f;

            cubeObj2.SetParent(cubeObj);
            cubeObj3.SetParent(cubeObj2);

            cubeObj3.GetComponent<Transform>().LocalPosition = new Vector3(0, 0, 5f);

            lightObj1.GetComponent<Light>().lightType = Light.LightType.Point;
            lightObj1.GetComponent<Transform>().Position = new Vector3(-5, 10, 0);
            lightObj1.GetComponent<Light>().range = 20f;
            lightObj1.GetComponent<Light>().intensity = 5f;
            lightObj1.GetComponent<Light>().color = new Color4(1f, 1f, 1f, 1f);

            lightObj2.GetComponent<Light>().lightType = Light.LightType.Spot;
            lightObj2.GetComponent<Transform>().Position = new Vector3(10, 0, 0);
            lightObj2.GetComponent<Light>().range = 20f;
            lightObj2.GetComponent<Light>().intensity = 5f;
            lightObj2.GetComponent<Light>().color = new Color4(1f, 0.5f, 1f, 1f);

            lightObj2.GetComponent<Light>().lightType = Light.LightType.Directional;
            lightObj2.GetComponent<Light>().shadowType = Light.ShadowType.Soft;
            lightObj2.GetComponent<Transform>().Position = new Vector3(10, 0, 0);
            lightObj2.GetComponent<Transform>().EulerAngles = new Vector3(-60, 35, 0);
            lightObj2.GetComponent<Light>().intensity = 1f;
            lightObj2.GetComponent<Light>().color = new Color4(1f, 1f, 1f, 1f);
            lightObj2.GetComponent<Light>().shadowResolution = Light.ShadowResolution.VeryHigh;

            var planeObj = new GameObject("plane");
            activeScene.MakeGameObject(planeObj);
            planeObj.AddComponent<MeshRenderer>();
            planeObj.AddComponent<MeshFilter>();
            planeObj.GetComponent<MeshFilter>().Mesh = Plane_mesh;
            planeObj.Transform.Position = new Vector3(0, -5, 0);
            planeObj.Transform.Scale = new Vector3(20, 1, 20);

            var CubeeObj = new GameObject("cube");
            activeScene.MakeGameObject(CubeeObj);
            CubeeObj.AddComponent<MeshRenderer>();
            CubeeObj.AddComponent<MeshFilter>();
            CubeeObj.GetComponent<MeshFilter>().Mesh = Cube_mesh;
            CubeeObj.Transform.Position = new Vector3(0, 0, 0);
            CubeeObj.Transform.Rotation = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(45), MathHelper.DegreesToRadians(45), MathHelper.DegreesToRadians(0));
            CubeeObj.Transform.Scale = new Vector3(2, 2, 2);

            var Eprob = new GameObject("Eprob");
            activeScene.MakeGameObject(Eprob);
            Eprob.AddComponent<EProbe>();
            Eprob.GetComponent<EProbe>().Far = 25f;
            Eprob.GetComponent<EProbe>().Near = 0.1f;
            Eprob.GetComponent<EProbe>().Resolution = 512;
            Eprob.Transform.Position = new Vector3(0, -2.5f, 0);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            EnsureRenderTargetSize();

            Scene? activeScene = ActiveScene;
            if (activeScene == null)
            {
                SwapBuffers();
                return;
            }

            GameObject? camObj = activeScene.Find(ActiveCameraName);
            Camera? mainCam = camObj?.GetComponent<Camera>();

            bool stressCaptureFrame = CameraStressTest.Enabled && CameraStressTest.Advance(camObj);

            activeScene.Render(args.Time);
            SortRenderQueue();

            Matrix4 view = mainCam?.ViewMatrix ?? Matrix4.Identity;
            Matrix4 projection = mainCam?.ProjectionMatrix ?? Matrix4.Identity;
            Matrix4 invProjection = Matrix4.Invert(projection);
            float nearPlane = mainCam?.NearClip ?? 0.01f;
            float farPlane = mainCam?.FarClip ?? 1000f;
            Vector3 mainCamPos = mainCam?.Transform?.Position ?? Vector3.Zero;

            // Shadow maps first: every shadow-casting light re-renders the opaque queue from its
            // own point of view. Must run before Forward+'s culling upload, since the resulting
            // per-light shadow index/matrix gets baked straight into the GPULight data it uploads.
            ShadowAssignment[] shadowAssignments = _shadowMapRenderer.Run(LightManager.AllData, RenderQueue, GetOrCreateDrawMesh, mainCamPos);

            // Forward+: depth pre-pass + tile light culling for the main camera's view. Leaves the
            // light SSBOs bound for every draw call below to read from directly (see
            // ForwardPlusRenderer's scope note re: RenderTexture cameras / EProbe faces reusing
            // this same result rather than getting their own culling pass).
            _forwardPlus.Run(RenderQueue, GetOrCreateDrawMesh, LightManager.AllData, shadowAssignments, view, projection, invProjection, nearPlane, farPlane);

            foreach (Camera cam in activeScene.FindAllComponents<Camera>())
            {
                if (cam.Target == null) continue;

                cam.Target.Bind();
                GL.Enable(EnableCap.DepthTest);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                FlushRenderQueue(cam.ViewMatrix, cam.ProjectionMatrix);

                cam.Target.Unbind();
            }

            _frameBuffer.Bind();
            GL.Enable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            FlushRenderQueue(view, projection);
            RenderQueue.Clear();

            if (stressCaptureFrame)
            {
                CameraStressTest.Capture(_forwardPlus, Size.X, Size.Y);
            }

            _frameBuffer.Unbind();

            GL.Disable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _frameBuffer.BindColorTexture(0);
            _screenQuad.Draw(_screenShader, 0);

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            currentProcess = Process.GetCurrentProcess();

            Input.CurrentWindow = this;
            Input.Update();

            DeltaTime = (float)args.Time;

            ActiveScene?.Tick(args.Time);

            LightManager.UpdateDynamicData();

            FixedUpdateTimer += (float)args.Time;
            FixedUpdateDataTimer += (float)args.Time;

            if (FixedUpdateTimer >= 0.02f)
            {
                FixedUpdate();
                FixedUpdateTimer = 0f;
            }

            if (FixedUpdateDataTimer >= 0.5f)
            {
                WindowTitle = $"{OriginalTitle} - Ram usage: {currentProcess.PrivateMemorySize64 / (1024L * 1024)}MB | Fps: {MathF.Floor(1f / DeltaTime)}";
                Title = WindowTitle;

                FixedUpdateDataTimer = 0f;
            }

            if (KeyboardState.IsKeyPressed(Keys.R))
            {
                Logger.Instance.Log("Reloading shaders");
                foreach (Shader shader in Shaders)
                {
                    shader.ReloadShader();
                    Logger.Instance.Log($"Reloaded shader '{shader.Name}'");
                }
            }

            if (KeyboardState.IsKeyPressed(Keys.Equal))
            {
                var camObj = ActiveScene?.Find(ActiveCameraName)?.Transform;
                if (camObj != null)
                    Logger.Instance.Log($"P:{camObj.Position} R:{camObj.Rotation}");
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (e.Width <= 0 || e.Height <= 0) return;

            GL.Viewport(0, 0, Size.X, Size.Y);
            WindowSize = new Vector2(Size.X, Size.Y);

            _frameBuffer?.Resize(new Vector2i(e.Width, e.Height));
            _forwardPlus?.Resize(new Vector2i(e.Width, e.Height));

            ActiveScene?.OnResize(new Vector2i(e.Width, e.Height));
        }

        // Keeps the color target and the Forward+ tile grid / depth pre-pass in lockstep with the
        // actual GL drawable size (Size). OnResize can report the logical ClientSize (e.g.
        // 1280x720) while the real drawable / framebuffer is larger (e.g. 1296x759, common under
        // window-manager/DPI scaling). Sizing the tile grid from the client size leaves the right
        // and bottom edges of the frame outside the allocated tile buffer, so those tile lookups
        // read out of bounds and render black. Comparing against Size here every frame makes the
        // whole Forward+ pipeline match the region gl_FragCoord actually spans.
        private void EnsureRenderTargetSize()
        {
            if (_frameBuffer == null || _forwardPlus == null) return;

            Vector2i drawable = new Vector2i(Size.X, Size.Y);
            if (drawable.X <= 0 || drawable.Y <= 0) return;

            _frameBuffer.Resize(drawable);
            _forwardPlus.Resize(drawable);
        }

        protected override void OnUnload()
        {
            SceneManager.UnloadAll();

            _frameBuffer.Dispose();
            _screenQuad.Dispose();
            _screenShader?.DeleteShader();
            _forwardPlus?.Dispose();
            _depthPrepassShader?.DeleteShader();
            _lightCullingShader?.DeleteShader();

            _shadowMapRenderer?.Dispose();
            _shadowDepthShader?.DeleteShader();
            _shadowCubeShader?.DeleteShader();

            foreach (var dm in _meshCache.Values) dm.Dispose();
            _meshCache.Clear();

            _shaderByUUID.Clear();
            TextureByUUID.Clear();
            MaterialByUUID.Clear();

            LightManager.Clear();
            ProbeManager.Clear();

            AssetLoader.UnloadAllAssets();

            base.OnUnload();
        }

        private void FixedUpdate()
        {
            Logger.Instance.PrintLogs();
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e) { base.OnKeyDown(e); ActiveScene?.OnKeyDown(e); }
        protected override void OnKeyUp(KeyboardKeyEventArgs e) { base.OnKeyUp(e); ActiveScene?.OnKeyUp(e); }
        protected override void OnMouseDown(MouseButtonEventArgs e) { base.OnMouseDown(e); ActiveScene?.OnMouseDown(e); }
        protected override void OnMouseUp(MouseButtonEventArgs e) { base.OnMouseUp(e); ActiveScene?.OnMouseUp(e); }
        protected override void OnMouseMove(MouseMoveEventArgs e) { base.OnMouseMove(e); ActiveScene?.OnMouseMove(e); }
        protected override void OnMouseWheel(MouseWheelEventArgs e) { base.OnMouseWheel(e); ActiveScene?.OnMouseWheel(e); }

        // -----------------------------------------------------------------------
        // Render queue
        // -----------------------------------------------------------------------
        public static void AddRenderRequest(SubMesh subMesh, Matrix4 modelMatrix, int sortLayer = 0, Layer objectLayer = Layer.Default)
        {
            var req = new RenderRequest(subMesh, modelMatrix, sortLayer, objectLayer);

            if (_capturingForProbe)
                ProbeCaptureQueue.Add(req);
            else
                RenderQueue.Add(req);
        }

        private void SortRenderQueue()
        {
            RenderQueue.Sort((a, b) =>
            {
                int layerCompare = a.SortLayer.CompareTo(b.SortLayer);
                if (layerCompare != 0) return layerCompare;
                if (a.Transparent && !b.Transparent) return 1;
                if (!a.Transparent && b.Transparent) return -1;
                return a.material.ShaderUUID.CompareTo(b.material.ShaderUUID);
            });
        }

        private DrawMesh GetOrCreateDrawMesh(SubMesh subMesh)
        {
            if (!_meshCache.TryGetValue(subMesh, out DrawMesh? drawMesh))
            {
                drawMesh = new DrawMesh(subMesh);
                _meshCache[subMesh] = drawMesh;
                subMesh.FreeVertexData();
            }
            return drawMesh;
        }

        private void FlushRenderQueue(Matrix4 view, Matrix4 projection, List<RenderRequest>? queueOverride = null)
        {
            var queue = queueOverride ?? RenderQueue;
            double totalTime = ActiveScene?.TotalTime ?? 0.0;

            int currentShaderUUID = int.MinValue;
            Shader? currentShader = null;
            bool haveShader = false;

            foreach (RenderRequest req in queue)
            {
                SubMesh subMesh = req.subMesh;

                // The queue is sorted by ShaderUUID (within layer/transparency groups), so
                // consecutive requests usually share a shader — only re-resolve it when the UUID
                // actually changes instead of doing a dictionary lookup every single request.
                if (!haveShader || req.material.ShaderUUID != currentShaderUUID)
                {
                    if (!_shaderByUUID.TryGetValue(req.material.ShaderUUID, out currentShader))
                        currentShader = MissingShader;
                    currentShaderUUID = req.material.ShaderUUID;
                    haveShader = true;
                }

                Shader? shader = currentShader;

                if (shader == null)
                {
                    Logger.Instance.LogError($"FlushRenderQueue: no shader for material '{req.material.Name}' and no MissingShader fallback.");
                    continue;
                }

                DrawMesh drawMesh = GetOrCreateDrawMesh(subMesh);

                switch (req.material.SeeThroughType)
                {
                    case Material._RenderType.Opaque:
                        GL.Disable(EnableCap.Blend);
                        break;
                    case Material._RenderType.Transparent:
                        GL.Enable(EnableCap.Blend);
                        break;
                }

                switch (req.material.cullMode)
                {
                    case Material._CullMode.CW:
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(CullFaceMode.Front);
                        break;
                    case Material._CullMode.CCW:
                        GL.Enable(EnableCap.CullFace);
                        GL.CullFace(CullFaceMode.Back);
                        break;
                    case Material._CullMode.Neither:
                        GL.Disable(EnableCap.CullFace);
                        break;
                }

                GL.DepthMask(req.material.ZWrite);

                // Tile grid width for the fragment shader's tileLights lookup (Forward+). Cheap
                // enough to set every draw; only actually changes on resize.
                if (shader.UniformNameSet.Contains("tileCountXF"))
                    shader.SetUniform("tileCountXF", (float)_forwardPlus.TileCountX);

                drawMesh.Draw(
                    shader,
                    req.ModelMatrix,
                    view,
                    projection,
                    req.material,
                    (float)totalTime,
                    req.ObjectLayer,
                    _shadowMapRenderer.ShadowMap2DHandle,
                    _shadowMapRenderer.ShadowMapCubeHandle
                );
            }
        }

        // -----------------------------------------------------------------------
        // RenderRequest
        // -----------------------------------------------------------------------

        public struct RenderRequest
        {
            public SubMesh subMesh;
            public Material material;
            public Matrix4 ModelMatrix;
            public bool Transparent;
            public int SortLayer;
            public Layer ObjectLayer;

            public RenderRequest(SubMesh subMesh, Matrix4 modelMatrix, int sortLayer = 0, Layer objectLayer = Layer.Default)
            {
                this.subMesh = subMesh;
                this.ModelMatrix = modelMatrix;
                this.SortLayer = sortLayer;
                this.ObjectLayer = objectLayer;

                MaterialByUUID.TryGetValue(subMesh.MaterialUUID, out var mat);
                this.material = mat ?? MissingMaterial;
                this.Transparent = material.SeeThroughType != Material._RenderType.Opaque;
            }
        }

        // -----------------------------------------------------------------------
        // Probe capture
        // -----------------------------------------------------------------------

        /// <summary>
        /// Re-traverses the scene (same as a normal render pass) with AddRenderRequest routed into
        /// ProbeCaptureQueue instead of the main RenderQueue. Call once per EProbe.UpdateProbe() —
        /// the resulting queue is reused across all 6 cubemap faces since geometry doesn't change
        /// between them, only view/projection do.
        /// </summary>
        public static void BeginProbeCapture()
        {
            Scene? activeScene = ActiveScene;
            if (activeScene == null) return;

            ProbeCaptureQueue.Clear();
            _capturingForProbe = true;
            activeScene.Render(activeScene.DeltaTime);
            _capturingForProbe = false;
        }

        /// <summary>Renders one probe cubemap face from ProbeCaptureQueue (populated by BeginProbeCapture).</summary>
        public static void RenderProbeFace(int fboHandle, int width, int height, Matrix4 view, Matrix4 projection)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboHandle);
            GL.Viewport(0, 0, width, height);
            GL.Enable(EnableCap.DepthTest);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Instance?.FlushRenderQueue(view, projection, ProbeCaptureQueue);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
    }
}