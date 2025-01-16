document.getElementById("sendQuery").addEventListener("click", async () => {
    const query = document.getElementById("query").value;
    const response = await chrome.runtime.sendMessage({ action: "queryCopilot", query });
    document.getElementById("response").textContent = JSON.stringify(response, null, 2);
});
