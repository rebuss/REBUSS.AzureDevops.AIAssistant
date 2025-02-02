using LibGit2Sharp;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Helpers;
using REBUSS.GitDaif.Service.API.Agents.Helpers;
using REBUSS.GitDaif.Service.API.DTO.Responses;
using REBUSS.GitDaif.Service.API.Properties;
using System.Diagnostics;

namespace REBUSS.GitDaif.Service.API.Agents
{
    public class BrowserCopilotForEnterprise : InterfaceAI
    {
        private readonly string modalWindowName;
        private readonly string userDataDir;
        private readonly string microsoftEdgePath;
        private readonly string microsoftAccount;
        private readonly ILogger<BrowserCopilotForEnterprise> logger;
        TaskCompletionSource<bool> sessionIsReady = new TaskCompletionSource<bool>();

        public BrowserCopilotForEnterprise(IOptions<CopilotSettings> copilotOptions, ILogger<BrowserCopilotForEnterprise> logger)
        {
            if (copilotOptions == null || copilotOptions.Value == null)
            {
                throw new ArgumentNullException(nameof(copilotOptions));
            }

            modalWindowName = copilotOptions.Value.ModalWindowName ?? throw new ArgumentNullException(nameof(modalWindowName));
            userDataDir = copilotOptions.Value.UserProfileDataDir ?? throw new ArgumentNullException(nameof(userDataDir));
            microsoftEdgePath = copilotOptions.Value.MsEdgePath ?? throw new ArgumentNullException(nameof(microsoftEdgePath));
            microsoftAccount = copilotOptions.Value.AccountName ?? throw new ArgumentNullException(nameof(microsoftAccount));
            this.logger = logger;
        }

        public static Task WaitForModalWindowAsync(string windowName)
        {
            var tcs = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                while (true)
                {
                    nint hWnd = NativeMethods.FindWindow(null, windowName);
                    if (hWnd != nint.Zero)
                    {
                        tcs.SetResult(true);
                        break;
                    }
                    Task.Delay(500).Wait();
                }
            });

            return tcs.Task;
        }

        public async Task<BaseResponse> AskAgent(string prompt, string filePath = null)
        {
            prompt = prompt.Replace("\n", "").Replace("\r", "");
            IBrowser browser = await OpenBrowser();
            
            try
            {
                browser.TargetChanged += OnTargetChanged;
                var pages = await browser.PagesAsync();
                var page = pages[0];
                await page.GoToAsync("https://m365.cloud.microsoft/chat");
                logger.LogInformation($"{nameof(BrowserCopilotForEnterprise)}: Waiting for https://m365.cloud.microsoft/chat - {DateTime.Now}");
                await sessionIsReady.Task.WithTimeout(30000);
                await SelectInput(page);
                await page.Keyboard.TypeAsync(prompt);
                await AddFileToChat(page, filePath);
                await Task.Delay(1000);
                await ClickButton(page, "button[type='submit']", "Submit", 15);
                await ClickButton(page, "button[class*='copy']", "Copy");
                await Task.Delay(2000);
                var text = NativeMethods.GetClipboardText();
                var response = new BaseResponse()
                {
                    Success = true,
                    Timestamp = DateTime.Now,
                    Message = text
                };

                await browser.CloseAsync();
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError("Error during communication with Copilot");
                await browser.CloseAsync();
                throw;
            }
        }

        private async Task<IBrowser> OpenBrowser()
        {
            try
            {
                return await LaunchBrowser();
            }
            catch (ProcessException ex)
            {
                logger.LogWarning(ex, "Microsoft Edge is already open. Attempting to close the browser and start a new session.");
                KillActivePuppeteerBrowsers();
            }

            return await LaunchBrowser();
        }

        private async Task<IBrowser> LaunchBrowser()
        {
            return await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = microsoftEdgePath,
                UserDataDir = userDataDir,
                Args = new[] { "--window-size=640,480" }
            });
        }

        private void KillActivePuppeteerBrowsers()
        {
            var edgeProcesses = Process.GetProcessesByName("msedge");

            foreach (var process in edgeProcesses)
            {
                try
                {
                    var commandLine = GetCommandLine(process);
                    if (!string.IsNullOrEmpty(commandLine) && (commandLine.Contains("--remote-debugging-port") || commandLine.Contains("--headless")))
                    {
                        logger.LogInformation($"Proces Edge (PID: {process.Id}) działa w trybie automatycznym i zostanie zamknięty.");
                        process.Kill();
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error for process {process.Id}: {ex.Message}");
                }
            }
        }

        private string GetCommandLine(Process process)
        {
            using (var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}"))
            {
                var query = searcher.Get().Cast<System.Management.ManagementObject>().FirstOrDefault();
                return query?["CommandLine"]?.ToString() ?? "";
            }
        }

        private async Task ClickButton(IPage page, string buttonSelector, string buttonDescription, int timeoutSec = 30)
        {
            var timeout = TimeSpan.FromSeconds(timeoutSec);
            var startTime = DateTime.Now;

            var frame = await GetCopilotIframe(page);

            while (DateTime.Now - startTime < timeout)
            {
                var button = await frame.QuerySelectorAsync(buttonSelector);
                if (button != null)
                {
                    var isDisabled = await button.EvaluateFunctionAsync<bool>("button => button.hasAttribute('disabled')");
                    if (!isDisabled)
                    {
                        await button.ClickAsync();
                        logger.LogInformation($"{buttonDescription} button was found, enabled, and clicked.");
                        return;
                    }
                    else
                    {
                        logger.LogInformation($"{buttonDescription} button is found but it is disabled, retrying...");
                    }
                }
                else
                {
                    logger.LogInformation($"{buttonDescription} button not found, retrying...");
                }

                await Task.Delay(1000);
            }

            logger.LogWarning($"{buttonDescription} button was not found or was disabled within the timeout period.");
        }

        private async Task SelectInput(IPage page)
        {
            var frame = await GetCopilotIframe(page);
            var inputElement = await frame.QuerySelectorAsync("[id*='input']");
            if (inputElement != null)
            {
                await inputElement.FocusAsync();
                logger.LogInformation("Element containing 'input' in its ID was found and focused.");
            }
            else
            {
                throw new NotFoundException("Element containing 'input' in its ID was not found.");
            }
        }

        private async Task AddFileToChat(IPage page, string filePath)
        {
            if (filePath != null)
            {
                logger.LogInformation($"Adding file {filePath}...");
            }
            else
            {
                logger.LogInformation("File path is empty.");
                return;
            }

            await ClickButton(page, "button[id*='attach']", "AttachFileButton");
            await WaitForModalWindowAsync(modalWindowName).WithTimeout(10000);

            nint hWndMain = NativeMethods.FindWindow(null, modalWindowName);
            if (hWndMain == nint.Zero)
            {
                logger.LogError($"{nameof(BrowserCopilotForEnterprise)}: Could not find the window!");
                return;
            }

            nint hWndEdit = NativeMethods.FindControlByClass(hWndMain, "Edit");
            if (hWndEdit == nint.Zero)
            {
                logger.LogError($"{nameof(BrowserCopilotForEnterprise)}: Could not find text input");
                return;
            }

            NativeMethods.SendMessage(hWndEdit, NativeMethods.WM_SETTEXT, nint.Zero, filePath);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYDOWN, nuint.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYUP, nuint.Zero);
        }

        private void OnTargetChanged(object sender, TargetChangedArgs e)
        {
            var url = e.Target?.Url ?? e.TargetInfo?.Url;
            if (!string.IsNullOrEmpty(url)
              && url.StartsWith("https://webshell.suite.office.com/iframe/TokenFactoryIframe")
              && url.Contains(microsoftAccount)) // the user is logged in
            {
                sessionIsReady.SetResult(true);
                logger.LogInformation($"{nameof(BrowserCopilotForEnterprise)}: Connected! {DateTime.Now}");
                var browser = sender as Browser;
                if (browser != null)
                {
                    browser.TargetChanged -= OnTargetChanged;
                }
            }
        }

        private async Task<IFrame> GetCopilotIframe(IPage page)
        {
            var iframes = page.Frames;
            foreach (var iframe in iframes)
            {
                var title = await iframe.EvaluateFunctionAsync<string>("() => document.title");
                if (title == "Copilot")
                {
                    logger.LogInformation("Iframe with title 'Copilot' was found.");
                    return iframe;
                }
            }

            throw new NotFoundException("Iframe with title 'Copilot' was not found.");
        }
    }
}
