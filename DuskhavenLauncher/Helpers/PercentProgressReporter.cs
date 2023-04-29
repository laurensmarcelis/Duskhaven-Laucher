using FastRsync.Diagnostics;
using System;

namespace DuskhavenLauncher.Helpers
{
    internal class PercentProgressReporter : IProgress<ProgressReport>
    {
        private ProgressOperationType currentOperation;

        private int progressPercentage;
        private Action<int> downloadProgressCallback;

        public PercentProgressReporter()
        {
        }

        public PercentProgressReporter(Action<int> progressCallback)
        {
            this.downloadProgressCallback = progressCallback;
        }

        public void Report(ProgressReport progress)
        {
            int num = (int)((double)progress.CurrentPosition / (double)progress.Total * 100.0 + 0.5);
            if (currentOperation != progress.Operation)
            {
                progressPercentage = -1;
                currentOperation = progress.Operation;
            }

            if (progressPercentage != num && num % 10 == 0)
            {
                progressPercentage = num;
                downloadProgressCallback(progressPercentage);
                Console.WriteLine("{0}: {1}%", currentOperation, num);
            }

           

        }
    }
}