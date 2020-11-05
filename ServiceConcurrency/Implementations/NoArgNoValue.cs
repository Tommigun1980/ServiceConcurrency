using System;
using System.Threading.Tasks;

namespace ServiceConcurrency
{
#pragma warning disable CS1591
    /// <summary>
    /// Prevents concurrent calls by allowing only one active call at a time,
    /// where concurrent calls will wait for the active call to finish.
    /// </summary>
    public sealed class NoArgNoValue
    {
        private Task runningTask;

        public async Task<StateInfo> ExecuteAndGetExecutorInfo(
            Func<Task> taskGetter)
        {
            if (this.runningTask != null)
            {
                await this.runningTask;
                return new StateInfo(false);
            }

            this.runningTask = taskGetter();
            try
            {
                await this.runningTask;

                return new StateInfo(true);
            }
            finally
            {
                this.runningTask = null;
            }
        }

        public Task Execute(
            Func<Task> taskGetter)
        {
            return this.ExecuteAndGetExecutorInfo(taskGetter);
        }

        public void Reset()
        {
            this.runningTask = null;
        }

        public bool IsExecuting()
        {
            return this.runningTask != null;
        }
    }
#pragma warning restore CS1591
}
