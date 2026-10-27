// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Screens;
using osu.Framework.Threading;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Overlays;

namespace AXPlayer.App.Screens
{
    public partial class Loader : AXPlayerScreen
    {
        public Loader()
        {
            ValidForResume = false;
        }

        private AXPlayerScreen loadableScreen;
        private ShaderPrecompiler precompiler;

        private AXPlayerLoadingSpinner spinner;
        private ScheduledDelegate spinnerShow;

        [Resolved]
        private LoadingOverlay loadingOverlay { get; set; }

        protected virtual AXPlayerScreen CreateLoadableScreen() => new MainAppView();

        protected virtual ShaderPrecompiler CreateShaderPrecompiler() => new ShaderPrecompiler();

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            spinnerShow = Scheduler.AddDelayed(loadingOverlay.Show, 200);

            LoadComponentAsync(precompiler = CreateShaderPrecompiler(), AddInternal);

            LoadComponentAsync(loadableScreen = CreateLoadableScreen());

            checkIfLoaded();
        }

        private void checkIfLoaded()
        {
            if (loadableScreen?.LoadState != LoadState.Ready || !precompiler.FinishedCompiling)
            {
                Schedule(checkIfLoaded);
                return;
            }

            spinnerShow?.Cancel();

            if (loadingOverlay.State.Value == Visibility.Visible)
            {
                loadingOverlay.Hide();
                this.Push(loadableScreen);
            }
            else
                this.Push(loadableScreen);
        }

        /// <summary>
        /// Compiles a set of shaders before continuing. Attempts to draw some frames between compilation by limiting to one compile per draw frame.
        /// </summary>
        public partial class ShaderPrecompiler : Drawable
        {
            private readonly List<IShader> loadTargets = new List<IShader>();

            public bool FinishedCompiling { get; private set; }

            [BackgroundDependencyLoader]
            private void load(ShaderManager manager)
            {
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.BLUR));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_3, FragmentShaderDescriptor.TEXTURE));

                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"ChromaticAberration"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"Greyscale"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, @"HueShift"));
                loadTargets.Add(manager.Load(VertexShaderDescriptor.TEXTURE_2, "Path"));
                loadTargets.Add(manager.Load("PathPrepass", "PathPrepass"));
            }

            protected virtual bool AllLoaded => loadTargets.All(s => s.IsLoaded);

            protected override void Update()
            {
                base.Update();

                // if our target is null we are done.
                if (AllLoaded)
                {
                    FinishedCompiling = true;
                    Expire();
                }
            }
        }
    }
}
