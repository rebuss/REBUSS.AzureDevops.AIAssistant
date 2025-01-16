document.addEventListener("DOMContentLoaded", () => {
    const pullRequestTab = document.querySelector(".pull-request-tab");
    if (pullRequestTab) {
        const button = document.createElement("button");
        button.textContent = "Rozszerzona funkcja";
        button.addEventListener("click", async () => {
            const result = await chrome.runtime.sendMessage({
                action: "queryLocalAPI",
                data: { pullRequestId: getPullRequestId() }
            });
            alert(`Wynik z lokalnego API: ${JSON.stringify(result)}`);
        });
        pullRequestTab.appendChild(button);
    }
});

function getPullRequestId() {
    const url = window.location.href;
    const match = url.match(/pullRequestId=(\d+)/);
    return match ? match[1] : null;
}
