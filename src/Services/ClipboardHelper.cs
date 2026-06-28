using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// STA-thread-safe clipboard helper for PowerToys Run plugin actions.
/// The Windows clipboard API requires the calling thread to be in STA mode,
/// which the PowerToys Run Action callback does not guarantee.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// Copies text to the clipboard, dispatching to an STA thread if necessary.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <returns>True if the copy succeeded, false otherwise.</returns>
    public static bool CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            // Try using WPF Application dispatcher first (available when PowerToys Run is active)
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher != null)
            {
                app.Dispatcher.Invoke(() =>
                {
                    System.Windows.Clipboard.SetText(text);
                });
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"PromptLibrary: WPF dispatcher clipboard failed, falling back to STA thread: {ex.Message}", typeof(ClipboardHelper));
        }

        // Fallback: spawn a dedicated STA thread with COMException retry logic
        try
        {
            bool success = false;
            Exception? threadException = null;

            var staThread = new Thread(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(text);
                        success = true;
                        threadException = null;
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        threadException = ex;
                        if (i < 2)
                        {
                            Thread.Sleep(50);
                        }
                    }
                    catch (Exception ex)
                    {
                        threadException = ex;
                        break;
                    }
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join(TimeSpan.FromSeconds(3));

            if (threadException != null)
            {
                Log.Error($"PromptLibrary: STA thread clipboard error: {threadException.Message}", typeof(ClipboardHelper));
                return false;
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error($"PromptLibrary: Clipboard copy failed completely: {ex.Message}", typeof(ClipboardHelper));
            return false;
        }
    }
}
