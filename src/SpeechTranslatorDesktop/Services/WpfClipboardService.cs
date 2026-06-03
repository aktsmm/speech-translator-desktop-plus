namespace SpeechTranslatorDesktop.Services;

public sealed class WpfClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        const int maxAttempts = 3;
        System.Runtime.InteropServices.ExternalException? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.CheckAccess())
                {
                    await dispatcher.InvokeAsync(() => System.Windows.Clipboard.SetText(text));
                }
                else
                {
                    System.Windows.Clipboard.SetText(text);
                }

                return;
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                lastException = ex;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException("The clipboard is busy. Try again.", lastException);
    }
}
