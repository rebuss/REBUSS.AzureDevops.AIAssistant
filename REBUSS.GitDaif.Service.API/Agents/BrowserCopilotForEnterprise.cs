using GitDaif.ServiceAPI;
using GitDaif.ServiceAPI.Agents;
using PuppeteerSharp;
using PuppeteerSharp.Helpers;
using REBUSS.GitDaif.Service.API.Agents.Helpers;
using REBUSS.GitDaif.Service.API.DTO.Responses;

namespace REBUSS.GitDaif.Service.API.Agents
{
    public class BrowserCopilotForEnterprise : InterfaceAI
    {
        private readonly string modalWindowName;
        private readonly string userDataDir;
        private readonly string microsoftEdgePath;
        private readonly string microsoftAccount;

        TaskCompletionSource<bool> sessionIsReady = new TaskCompletionSource<bool>();

        public BrowserCopilotForEnterprise(IConfiguration config)
        {
            modalWindowName = config[ConfigConsts.ModalWindowName] ?? throw new ArgumentNullException(nameof(modalWindowName));
            userDataDir = config[ConfigConsts.UserProfileDataDir] ?? throw new ArgumentNullException(nameof(userDataDir));
            microsoftEdgePath = config[ConfigConsts.EdgePath] ?? throw new ArgumentNullException(nameof(microsoftEdgePath));
            microsoftAccount = config[ConfigConsts.MicrosoftAccount] ?? throw new ArgumentNullException(nameof(microsoftAccount));
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
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = microsoftEdgePath,
                UserDataDir = userDataDir
            });

            browser.TargetChanged += OnTargetChanged;

            var pages = await browser.PagesAsync();

            var page = pages[0];
            await page.GoToAsync("https://m365.cloud.microsoft/chat");
            await sessionIsReady.Task;
            await page.FocusAsync("#ms-searchux-input-0");
            await page.Keyboard.PressAsync("Enter");
            await page.Keyboard.TypeAsync(prompt);

            if (filePath != null)
            {
                await AddFileToChat(page, filePath);
            }
            else
            {
                await page.Keyboard.PressAsync("Enter");
            }

            return null;
        }

        private async Task AddFileToChat(IPage page, string filePath)
        {
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.PressAsync("Enter");
            await WaitForModalWindowAsync(modalWindowName).WithTimeout(10000);

            nint hWndMain = NativeMethods.FindWindow(null, modalWindowName);
            if (hWndMain == nint.Zero)
            {
                Console.WriteLine("Could not find the window!");
                return;
            }

            nint hWndEdit = NativeMethods.FindControlByClass(hWndMain, "Edit");
            if (hWndEdit == nint.Zero)
            {
                Console.WriteLine("Could not find text input");
                return;
            }

            NativeMethods.SendMessage(hWndEdit, NativeMethods.WM_SETTEXT, nint.Zero, filePath);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYDOWN, nuint.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_RETURN, 0, NativeMethods.KEYEVENTF_KEYUP, nuint.Zero);

            await page.FocusAsync("#ms-searchux-input-0");
            await page.Keyboard.PressAsync("Enter");
        }

        private void OnTargetChanged(object sender, TargetChangedArgs e)
        {
            var url = e.Target?.Url ?? e.TargetInfo?.Url;
            if (!string.IsNullOrEmpty(url)
              && url.StartsWith("https://webshell.suite.office.com/iframe/TokenFactoryIframe")
              && url.Contains(microsoftAccount)) // the user is logged in
            {
                sessionIsReady.SetResult(true);
                Console.WriteLine($"Connected! {DateTime.Now}");
                var browser = sender as Browser;
                if (browser != null)
                {
                    browser.TargetChanged -= OnTargetChanged;
                }
            }
        }
    }
}
