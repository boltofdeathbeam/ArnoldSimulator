﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArnoldUI.Properties;
using GoodAI.Arnold.Graphics;
using GoodAI.Arnold.Graphics.Models;
using GoodAI.Arnold.Properties;
using GoodAI.Arnold.Simulation;
using GoodAI.Arnold.OpenTKExtensions;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK.Input;
using QuickFont;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace GoodAI.Arnold.Forms
{
    public partial class VisualizationForm : Form
    {
        //public const float FrameMilliseconds = 1000f/60;

        public const float NearZ = 1;
        public const float FarZ = 2048;

        public const float MouseSlowFactor = 2;

        public const int GridWidth = 100;
        public const int GridDepth = 100;
        public const int GridCellSize = 10;

        private readonly Color m_backgroundColor = Color.FromArgb(255, 30, 30, 30);

        private readonly Stopwatch m_stopwatch = new Stopwatch();

        private float m_keyRight;
        private float m_keyLeft;
        private float m_keyForward;
        private float m_keyBack;
        private float m_keyUp;
        private float m_keyDown;

        private float m_fps;

        private bool m_mouseCaptured;
        private Vector2 m_lastMousePosition;

        private BrainSimulation m_brainSimulation;

        public Matrix4 ProjectionMatrix { get; set; }

        private readonly Camera m_camera;
        private readonly GridModel m_gridModel;
        private readonly CompositeModelBase<ModelBase> m_brainModel;

        private readonly IList<ModelBase> m_models = new List<ModelBase>();
        private PickRay m_pickRay;
        private readonly Dictionary<ModelBase, float> m_translucentDistanceCache = new Dictionary<ModelBase, float>();
        private QFont m_font;
        private int m_modelsDisplayed;

        private readonly ISet<ExpertModel> m_pickedExperts = new HashSet<ExpertModel>();

        public BrainSimulation BrainSimulation
        {
            get { return m_brainSimulation; }
            set
            {
                // TODO: Cleanup the old simulation?
                m_brainModel.Clear();
                m_brainSimulation = value;
                // TODO: Nasty! Change!
                foreach (var region in m_brainSimulation.Regions)
                {
                    foreach (ExpertModel expert in region.Experts)
                        expert.Camera = m_camera;

                    m_brainModel.AddChild(region);
                }
            }
        }

        public VisualizationForm()
        {
            InitializeComponent();

            m_camera = new Camera
            {
                Position = new Vector3(5, 100, 100),
                Orientation = new Vector3((float) Math.PI, (float) (-Math.PI/4), 0)
            };

            m_gridModel = new GridModel(GridWidth, GridDepth, GridCellSize);

            m_models.Add(m_gridModel);

            m_brainModel = new BrainModel();

            m_models.Add(m_brainModel);
        }

        // Resize the glControl
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            glControl.Resize += glControl_Resize;
            glControl.MouseUp += glControl_MouseUp;

            GL.ClearColor(m_backgroundColor);

            // Heh. Blending is a pain with this.
            //GL.Enable(EnableCap.DepthTest);

            Application.Idle += Application_Idle;

            glControl_Resize(glControl, EventArgs.Empty);

            glControl.Context.SwapInterval = 1;

            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            LoadSprites();

            ResetLastMousePosition();

            m_font = new QFont(new Font(FontFamily.GenericMonospace, 14))
            {
                Options =
                {
                    Colour = Color4.GreenYellow
                }
            };

            m_stopwatch.Start();
        }

        private void LoadSprites()
        {
            GL.GenTextures(1, out ExpertModel.NeuronTexture);
            GL.BindTexture(TextureTarget.Texture2D, ExpertModel.NeuronTexture);

            Bitmap bitmap = Resources.BasicNeuron;

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            bitmap.UnlockBits(data);
            bitmap.Dispose();

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        }

        private void glControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                m_mouseCaptured = !m_mouseCaptured;
                
                if (m_mouseCaptured)
                    HideCursor();
                else
                    ShowCursor();
            }


            if (m_mouseCaptured)
                ResetLastMousePosition();

            if (!m_mouseCaptured && e.Button == MouseButtons.Left)
                PickObject(e.X, glControl.Size.Height - e.Y);  // Invert Y (windows 0,0 is top left, GL is bottom left).
        }

        private void PickObject(int x, int y)
        {
            PickRay pickRay = GetPickRay(x, y);

            m_pickRay = pickRay;

            ExpertModel expert = FindFirstExpert(pickRay, BrainSimulation.Regions);
            if (expert != null)
                PickExpert(expert);
        }

        private void PickExpert(ExpertModel expert)
        {
            expert.Picked = !expert.Picked;
            if (expert.Picked)
                m_pickedExperts.Add(expert);
            else
                m_pickedExperts.Remove(expert);
        }

        private PickRay GetPickRay(int x, int y)
        {
            float normX = (2f * x) / glControl.Size.Width - 1f;
            float normY = (2f * y) / glControl.Size.Height - 1f;

            Vector4 clipRay = new Vector4(normX, normY, -1, 0);

            Vector4 eyeRay = Vector4.Transform(clipRay, ProjectionMatrix.Inverted());
            eyeRay = new Vector4(eyeRay.X, eyeRay.Y, -1, 0);

            
            Vector3 worldRay = Vector4.Transform(eyeRay, m_camera.CurrentFrameViewMatrix.Inverted()).Xyz.Normalized;

            return new PickRay
            {
                Position = m_camera.Position,
                Direction = worldRay,
                Length = 100f
            };
        }

        private Vector3 ModelToScreenCoordinates(ModelBase model, out bool isBehindCamera)
        {
            Vector2 projected = Project(model, out isBehindCamera);
            return new Vector3(projected.X, glControl.Size.Height - projected.Y, 0);
        }

        /// <summary>
        /// Project the center of the given model onto screen coordinates.
        /// </summary>
        private Vector2 Project(ModelBase model, out bool isBehindCamera)
        {
            // TODO(HonzaS): Allow different points than centers?
            var center4 = new Vector4(Vector3.Zero, 1);

            // Transform the to clip space.
            Vector4 world = Vector4.Transform(center4, model.CurrentWorldMatrix);
            Vector4 view = Vector4.Transform(world, m_camera.CurrentFrameViewMatrix);
            Vector4 clip = Vector4.Transform(view, ProjectionMatrix);

            // TODO: Change this to something less hacky.
            isBehindCamera = clip.Z < 0;

            // Transform to screen space.
            Vector3 ndc = clip.Xyz/clip.W;

            Vector2 screen = ((ndc.Xy + Vector2.One)/2f) * new Vector2(glControl.Size.Width, glControl.Size.Height);

            return screen;
        }

        private ExpertModel FindFirstExpert(PickRay pickRay, List<RegionModel> regions)
        {
            float closestDistance = float.MaxValue;
            ExpertModel closestExpert = null;
            foreach (RegionModel region in regions)
            {
                foreach (ExpertModel expert in region.Experts)
                {
                    float distance = expert.DistanceToRayOrigin(pickRay);
                    if (distance < closestDistance)
                    {
                        closestExpert = expert;
                        closestDistance = distance;
                    }
                }
            }

            return closestExpert;
        }

        private void ResetLastMousePosition()
        {
            MouseState state = Mouse.GetState();
            m_lastMousePosition = new Vector2(state.X, state.Y);
        }

        private void HandleKeyboard()
        {
            if (!glControl.Focused)
                return;

            KeyboardState keyboardState = Keyboard.GetState();

            // TODO: Implement, duh.
            //if (keyboardState.IsKeyDown(Key.Escape))
            //    StopSimulation()

            m_keyLeft = keyboardState.IsKeyDown(Key.A) ? 1 : 0;
            m_keyRight = keyboardState.IsKeyDown(Key.D) ? 1 : 0;
            m_keyForward = keyboardState.IsKeyDown(Key.W) ? 1 : 0;
            m_keyBack = keyboardState.IsKeyDown(Key.S) ? 1 : 0;
            m_keyUp = keyboardState.IsKeyDown(Key.Space) ? 1 : 0;
            m_keyDown = keyboardState.IsKeyDown(Key.C) ? 1 : 0;
        }

        void Application_Idle(object sender, EventArgs e)
        {
            if (glControl.IsDisposed)
                return;

            while (glControl.IsIdle)
                Step();
        }


        private void Step()
        {
            m_stopwatch.Stop();
            float elapsedMs = m_stopwatch.ElapsedMilliseconds;
            m_stopwatch.Reset();
            m_stopwatch.Start();

            m_fps = 1000/elapsedMs;

            if (m_mouseCaptured)
            {
                Vector2 delta = (m_lastMousePosition - new Vector2(Mouse.GetState().X, Mouse.GetState().Y))/MouseSlowFactor;
                m_lastMousePosition += delta;

                if (delta != Vector2.Zero)
                    m_camera.AddRotation(delta.X, delta.Y, elapsedMs);

                Mouse.SetPosition(Left + glControl.Size.Width / 2, Top + glControl.Size.Height / 2);
                m_lastMousePosition = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
            }

            HandleKeyboard();

            UpdateFrame(elapsedMs);

            RenderFrame(elapsedMs);
        }

        private void glControl_Resize(object sender, EventArgs e)
        {
            GLControl c = sender as GLControl;

            if (c.Size.Height == 0)
                c.Size = new Size(c.Size.Width, 1);
        }

        private void HideCursor()
        {
            Cursor = new Cursor(Resources.EmptyCursor.Handle);
        }

        private void ShowCursor()
        {
            Cursor = Cursors.Default;
        }

        private void UpdateFrame(float elapsedMs)
        {
            foreach (ModelBase model in m_models)
                model.Update(elapsedMs);

            bool isSlow = Keyboard.GetState().IsKeyDown(Key.ControlLeft);
            m_camera.Move(m_keyRight - m_keyLeft, m_keyUp - m_keyDown, m_keyForward - m_keyBack, elapsedMs, isSlow);
        }

        private void RenderFrame(float elapsedMs)
        {
            RenderBegin();

            RenderScene(elapsedMs);

            RenderOverlay();

            RenderEnd();
        }

        private void RenderScene(float elapsedMs)
        {
            List<ModelBase> opaqueModels = new List<ModelBase>();
            List<ModelBase> translucentModels = new List<ModelBase>();

            // TODO: Do this only when necessary.
            foreach (ModelBase model in m_models)
                CollectModels(model, ref opaqueModels, ref translucentModels);

            // TODO: Only if the camera is moved.
            SortModels(translucentModels);

            m_modelsDisplayed = opaqueModels.Count + translucentModels.Count;

            // Debug only.
            m_pickRay?.Render(m_camera, elapsedMs);

            // Render here.
            foreach (ModelBase model in opaqueModels)
                model.Render(elapsedMs);

            foreach (ModelBase model in translucentModels)
                model.Render(elapsedMs);
        }

        private void RenderOverlay()
        {
            if (m_font == null)
                return;

            // The fonts setup the same projection, but we also need to draw rectangles etc.
            // So we'll set up our own on the bottom of theirs.
            SetupOverlayProjection();

            RenderPickedInfo();

            RenderDiagnostics();

            // Tear down our projection in case there is some more drawing happening.
            TeardownOverlayProjection();
        }

        private void RenderPickedInfo()
        {
            foreach (ExpertModel expert in m_pickedExperts)
            {
                bool isBehindCamera;
                Vector3 screenPosition = ModelToScreenCoordinates(expert, out isBehindCamera);

                if (isBehindCamera)
                    continue;

                RenderExpertInfo(expert, screenPosition);
            }
        }

        private void RenderExpertInfo(ExpertModel expert, Vector3 screenPosition)
        {
            screenPosition.X += 10;
            screenPosition.Y += 10;

            using (Blender.AveragingBlender())
            {
                GL.PushMatrix();
                GL.Translate(screenPosition);

                GL.Color4(new Color4(100, 100, 100, 220));
                GL.Begin(PrimitiveType.Quads);

                GL.Vertex3(0, 0, 0);
                GL.Vertex3(150, 0, 0);
                GL.Vertex3(150, 30, 0);
                GL.Vertex3(0, 30, 0);

                GL.End();
                GL.PopMatrix();

                QFont.Begin();
                GL.Translate(10, 0, 0);
                GL.Translate(screenPosition);

                m_font.Print($"{expert.Position}", QFontAlignment.Left);

                QFont.End();
            }
        }

        private void SetupOverlayProjection()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix(); //push projection matrix
            GL.LoadIdentity();
            GL.Ortho(0, glControl.Size.Width, glControl.Size.Height, 0, -1.0, 1.0);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();  //push modelview matrix
            GL.LoadIdentity();
        }

        private void TeardownOverlayProjection()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix(); //pop modelview

            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix(); //pop projection

            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void RenderDiagnostics()
        {
            QFont.Begin();
            GL.PushMatrix();

            GL.Translate(m_font.MonoSpaceWidth, 0, 0);
            m_font.Print($"fps: {(int) m_fps}", QFontAlignment.Left);

            GL.Translate(0, m_font.LineSpacing, 0);
            m_font.Print($"# of models: {m_modelsDisplayed}", QFontAlignment.Left);

            GL.PopMatrix();
            QFont.End();
        }

        private void SortModels(List<ModelBase> models)
        {
            foreach (ModelBase model in models)
                m_translucentDistanceCache[model] = model.CurrentWorldMatrix.ExtractTranslation().DistanceFrom(m_camera.Position);

            models.Sort(
                (model1, model2) => m_translucentDistanceCache[model1] < m_translucentDistanceCache[model2]
                    ? 1
                    : m_translucentDistanceCache[model1] > m_translucentDistanceCache[model2] ? -1 : 0);
        }

        private static void CollectModels(ModelBase model, ref List<ModelBase> opaqueModels, ref List<ModelBase> translucentModels)
        {
            if (!model.Visible)
                return;

            model.UpdateCurrentWorldMatrix();

            var compositeModel = model as ICompositeModel;
            if (compositeModel != null)
                foreach (ModelBase child in compositeModel.Models)
                    CollectModels(child, ref opaqueModels, ref translucentModels);

            if (model.Translucent)
                translucentModels.Add(model);
            else
                opaqueModels.Add(model);
        }

        private void RenderBegin()
        {
            SetUpProjection();

            SetUpView();

            // QFont leaves the last texture bound.
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void SetUpView()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            m_camera.UpdateCurrentFrameMatrix();
            Matrix4 viewMatrix = m_camera.CurrentFrameViewMatrix;

            GL.LoadMatrix(ref viewMatrix);
        }

        private void SetUpProjection()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Viewport(0, 0, glControl.Size.Width, glControl.Size.Height); // Use all of the glControl painting area
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspectRatio = glControl.Size.Width / (float)glControl.Size.Height;
            ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), aspectRatio, NearZ, FarZ);
            Matrix4 perspective = ProjectionMatrix;
            GL.LoadMatrix(ref perspective);
        }

        private void RenderEnd()
        {
            GL.Flush();

            glControl.SwapBuffers();
        }
    }
}
