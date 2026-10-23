// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual
{
    public abstract partial class AXPlayerTestScene : TestScene
    {
        protected override ITestSceneTestRunner CreateRunner() => new AXPlayerTestSceneTestRunner();

        private partial class AXPlayerTestSceneTestRunner : AXPlayerAppBase, ITestSceneTestRunner
        {
            private TestSceneTestRunner.TestRunner runner;

            protected override void LoadAsyncComplete()
            {
                base.LoadAsyncComplete();
                Add(runner = new TestSceneTestRunner.TestRunner());
            }

            public void RunTestBlocking(TestScene test) => runner.RunTestBlocking(test);
        }
    }
}
