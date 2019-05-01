namespace TS3AudioBot.Helper.Environment
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;

	public class SystemMonitor
	{
		private static readonly Process CurrentProcess = Process.GetCurrentProcess();
		private readonly ReaderWriterLockSlim historyLock = new ReaderWriterLockSlim();
		private readonly Queue<SystemMonitorSnapshot> history = new Queue<SystemMonitorSnapshot>();
		private TickWorker ticker = null;

		private bool historyChanged = true;
		private SystemMonitorReport lastReport = null;
		private DateTime lastSnapshotTime = DateTime.MinValue;
		private TimeSpan lastCpuTime = TimeSpan.Zero;

		public DateTime StartTime { get; } = Util.GetNow();

		public void StartTimedSnapshots()
		{
			if (ticker != null)
				throw new InvalidOperationException("Ticker already running");
			ticker = TickPool.RegisterTick(CreateSnapshot, TimeSpan.FromSeconds(1), true);
		}

		public void CreateSnapshot()
		{
			CurrentProcess.Refresh();

			//TODO: foreach (ProcessThread thread in CurrentProcess.Threads)
			{
			}

			var currentSnapshotTime = Util.GetNow();
			var currentCpuTime = CurrentProcess.TotalProcessorTime;

			var timeDiff = currentSnapshotTime - lastSnapshotTime;
			var cpuDiff = currentCpuTime - lastCpuTime;
			var cpu = (cpuDiff.Ticks / (float)timeDiff.Ticks);

			lastSnapshotTime = currentSnapshotTime;
			lastCpuTime = currentCpuTime;

			historyLock.EnterWriteLock();
			try
			{
				history.Enqueue(new SystemMonitorSnapshot
				{
					Memory = CurrentProcess.WorkingSet64,
					Cpu = cpu,
				});

				while (history.Count > 60)
					history.Dequeue();

				historyChanged = true;
			}
			finally
			{
				historyLock.ExitWriteLock();
			}
		}

		public SystemMonitorReport GetReport()
		{
			try
			{
				historyLock.EnterReadLock();
				if (historyChanged || lastReport == null)
				{
					lastReport = new SystemMonitorReport
					{
						Memory = history.Select(x => x.Memory).ToArray(),
						Cpu = history.Select(x => x.Cpu).ToArray(),
					};
					historyChanged = false;
				}
				return lastReport;
			}
			finally
			{
				historyLock.ExitReadLock();
			}
		}
	}

	public class SystemMonitorReport
	{
		public long[] Memory { get; set; }
		public float[] Cpu { get; set; }
	}

	public struct SystemMonitorSnapshot
	{
		public float Cpu { get; set; }
		public long Memory { get; set; }
	}
}
