using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using WinUIShared.Enums;
using WinUIShared.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace WinUIShared.Controls
{
    public sealed partial class ProcessManager : UserControl
    {
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State),
            typeof(OperationState),
            typeof(ProcessManager),
            new PropertyMetadata(OperationState.BeforeOperation, OnStateChanged));
        public OperationState State
        {
            get => (OperationState)GetValue(StateProperty);
            set { if (State != value) SetValue(StateProperty, value); }
        }
        public event EventHandler<OperationState>? StateChanged;
        public event RoutedEventHandler? DoneRequested;

        public static readonly DependencyProperty ProcessorProperty = DependencyProperty.Register(
            nameof(Processor),
            typeof(Processor),
            typeof(ProcessManager),
            new PropertyMetadata(null, OnProcessorChanged));

        public Processor Processor
        {
            get => (Processor)GetValue(ProcessorProperty);
            set => SetValue(ProcessorProperty, value);
        }

        public static readonly DependencyProperty HardwareSelectorProperty = DependencyProperty.Register(
            nameof(HardwareSelector),
            typeof(HardwareSelector),
            typeof(ProcessManager),
            new PropertyMetadata(null));

        public HardwareSelector HardwareSelector
        {
            get => (HardwareSelector)GetValue(HardwareSelectorProperty);
            set => SetValue(HardwareSelectorProperty, value);
        }

        public static readonly DependencyProperty OnlyPrimaryProperty = DependencyProperty.Register(
            nameof(OnlyPrimary),
            typeof(bool),
            typeof(ProcessManager),
            new PropertyMetadata(false));

        public bool OnlyPrimary
        {
            get => (bool)GetValue(OnlyPrimaryProperty);
            set => SetValue(OnlyPrimaryProperty, value);
        }

        private bool processFailed;
        // Assigned by the error reporter together with processFailed.
        private string processFailedMessage = null!;

        public ProcessManager()
        {
            InitializeComponent();
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var manager = (ProcessManager)d;
            manager.StateChanged?.Invoke(manager, manager.State);
        }

        private static void OnProcessorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var manager = (ProcessManager)d;
            if (manager.Processor == null) return;
            manager.Processor.SetProgressAndErrorReporters(
                new Progress<string>(s => manager.ProcessProgress.LeftTextPrimary = s),
                new Progress<string>(s => manager.ProcessProgress.CenterTextPrimary = s),
                new Progress<string>(s => manager.ProcessProgress.RightTextPrimary = manager.Processor.StopRequested
                    ? manager.State == OperationState.AfterOperation ? "Stopped — output saved" : "Stopping — saving output…"
                    : s),
                new Progress<double>(d => manager.ProcessProgress.ProgressPrimary = d),
                new Progress<string>(s => manager.ProcessProgress.LeftTextSecondary = s),
                new Progress<string>(s => manager.ProcessProgress.CenterTextSecondary = s),
                new Progress<string>(s => manager.ProcessProgress.RightTextSecondary = s),
                new Progress<double>(d => manager.ProcessProgress.ProgressSecondary = d),
                s =>
                {
                    manager.processFailed = true;
                    manager.processFailedMessage = s;
                }
            );
        }

        private void ProcessProgress_OnPauseRequested(object? sender, EventArgs e)
        {
            Processor.Pause();
        }

        private void ProcessProgress_OnResumeRequested(object? sender, EventArgs e)
        {
            Processor.Resume();
        }

        private void ProcessProgress_OnViewRequested(object? sender, EventArgs e)
        {
            Processor.ViewFile();
        }

        private void ProcessProgress_OnStopRequested(object? sender, EventArgs e)
        {
            ProcessProgress.RightTextPrimary = "Stopping — saving output…";
            Processor.Stop();
        }

        private async void ProcessProgress_OnCancelRequested(object? sender, EventArgs e)
        {
            await Processor.Cancel();
            State = OperationState.BeforeOperation;
        }

        private void ProcessProgress_OnDoneRequested(object sender, RoutedEventArgs e)
        {
            DoneRequested?.Invoke(this, e);
        }

        public async Task<string?> StartProcess(Func<Task> process)
        {
            SetupProcess();

            var memoryFailure = false;
            try
            {
                var gpu = await HardwareSelector.ResolveSelectedGpuAsync(Processor.FfmpegPath);
                if (gpu != null) Processor.EnableHardwareAccelParams(gpu);
                else Processor.DisableHardwareAccel();
                await process();
            }
            catch (Exception exception) when (exception is OutOfMemoryException ||
                exception is Win32Exception { NativeErrorCode: 8 or 14 or 1450 or 1455 })
            {
                await Processor.Cancel();
                memoryFailure = true;
                processFailed = true;
                processFailedMessage = $"The crop stopped because Windows could not allocate enough memory. Free memory before clicking Crop again.\n\n{exception}";
            }

            if (State == OperationState.BeforeOperation) return null; //Canceled
            if (processFailed)
            {
                State = OperationState.BeforeOperation;
                var reportPath = VideoCropper.App.Diagnostics.Report("Crop operation failed", processFailedMessage);
                await ErrorAction($"{processFailedMessage}\n\nDiagnostic report: {reportPath}", memoryFailure || Processor.MemoryExhausted);
                return null;
            }

            State = OperationState.AfterOperation;
            ProcessProgress.RightTextPrimary = Processor.StopRequested ? "Stopped — output saved" : "Done";
            VideoCropper.App.Diagnostics.Log(Processor.StopRequested ? "Crop stopped; output saved" : "Crop operation completed", Processor.GetFile());
            return Processor.GetFile();

            async Task ErrorAction(string message, bool memoryExhausted)
            {
                processFailedMessage = message;
                ErrorDialog.Title = memoryExhausted ? "Not enough memory to crop" : "Operation failed";
                ErrorDialog.Content = new ScrollViewer
                {
                    MaxHeight = 400,
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
                };
                await ErrorDialog.ShowAsync();
            }
        }

        private void CopyErrorDetails(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var data = new DataPackage();
            data.SetText(processFailedMessage);
            Clipboard.SetContent(data);
            args.Cancel = true;
        }

        private void SetupProcess()
        {
            State = OperationState.DuringOperation;
            processFailed = false;
            ProcessProgress.LeftTextPrimary = string.Empty;
            ProcessProgress.CenterTextPrimary = string.Empty;
            ProcessProgress.RightTextPrimary = string.Empty;
            ProcessProgress.ProgressPrimary = 0;
            ProcessProgress.LeftTextSecondary = string.Empty;
            ProcessProgress.CenterTextSecondary = string.Empty;
            ProcessProgress.RightTextSecondary = string.Empty;
            ProcessProgress.ProgressSecondary = 0;
        }
    }
}
