chrome.runtime.onMessage.addListener(async (message, sender, sendResponse) => {
    if (message.action === "queryCopilot") {
        const authToken = await getAuthToken();
        const response = await fetch("https://copilot.microsoft.com/api/chat", {
            method: "POST",
            headers: {
                "Authorization": `Bearer ${authToken}`,
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ query: message.query })
        });
        const result = await response.json();
        sendResponse(result);
    } else if (message.action === "queryLocalAPI") {
        const response = await fetch("http://localhost:5000/api", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(message.data)
        });
        const result = await response.json();
        sendResponse(result);
    }
    return true;
});

async function getAuthToken() {
    return new Promise((resolve, reject) => {
        chrome.identity.getAuthToken({ interactive: true }, (token) => {
            if (chrome.runtime.lastError) {
                reject(chrome.runtime.lastError);
            } else {
                resolve(token);
            }
        });
    });
}
