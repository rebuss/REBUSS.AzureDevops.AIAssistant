"use strict";
var _a, _b, _c;
let prId = null;
let organizationName = null;
let repositoryName = null;
let projectName = null;
let currentFile = null;
(_a = document.getElementById("summarizePr")) === null || _a === void 0 ? void 0 : _a.addEventListener("click", summarizePr);
(_b = document.getElementById("reviewPr")) === null || _b === void 0 ? void 0 : _b.addEventListener("click", reviewPr);
// document.getElementById("reviewFile")?.addEventListener("click", reviewFile);
(_c = document.getElementById("getDiff")) === null || _c === void 0 ? void 0 : _c.addEventListener("click", getDiff);
document.addEventListener("DOMContentLoaded", async () => {
    let [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tab && tab.url) {
        prId = extractPullRequestId(tab.url);
        organizationName = extractOrganizationName(tab.url);
        repositoryName = extractRepositoryName(tab.url);
        projectName = extractProjectName(tab.url);
    }
    if (tab
        && tab.url
        && tab.url.includes("dev.azure.com")
        && tab.url.includes("/pullrequest/")) {
        enableDevOpsButtons();
        // observeFileChanges();
        console.log("Pull Request is active");
    }
    else {
        console.log("PR is inactive");
        disableDevOpsButtons();
        // disableReviewFileButton();
    }
});
// Enables buttons when on a valid Pull Request page
function enableDevOpsButtons() {
    document.querySelectorAll(".disable-on-no-devops").forEach((btn) => {
        btn.disabled = false;
        btn.style.opacity = "1";
        btn.style.cursor = "pointer";
    });
}
// Disables buttons when not on a valid Pull Request page
function disableDevOpsButtons() {
    document.querySelectorAll(".disable-on-no-devops").forEach((btn) => {
        btn.disabled = true;
        btn.style.opacity = "0.5";
        btn.style.cursor = "not-allowed";
    });
}
// Extracts Pull Request ID from the URL
function extractPullRequestId(url) {
    const match = url.match(/\/pullrequest\/(\d+)/);
    return match ? match[1] : null;
}
// Extracts organization name from the URL (e.g. dev.azure.com/organization)
function extractOrganizationName(url) {
    const match = url.match(/dev\.azure\.com\/([^\/]+)/);
    return match ? match[1] : null;
}
// Extracts project name from the URL (e.g. dev.azure.com/organization/project)
function extractProjectName(url) {
    const match = url.match(/dev\.azure\.com\/[^\/]+\/([^\/]+)/);
    return match ? match[1] : null;
}
// Extracts repository name from the URL (e.g. dev.azure.com/organization/project/_git/repository)
function extractRepositoryName(url) {
    const match = url.match(/dev\.azure\.com\/[^\/]+\/[^\/]+\/_git\/([^\/]+)/);
    return match ? match[1] : null;
}
// Sends a request to summarize the PR
function summarizePr() {
    sendRequestToLocalAPI(`Summarize`);
}
// Sends a request to review the PR
function reviewPr() {
    sendRequestToLocalAPI(`Review`);
}
// Sends a request to get the diff file for the PR
function getDiff() {
    sendRequestToLocalAPI(`GetDiffFile`, true);
}
function showProgressRing() {
    const progressRing = document.getElementById("progressRing");
    if (progressRing) {
        progressRing.style.display = "block";
    }
}
function hideProgressRing() {
    const progressRing = document.getElementById("progressRing");
    if (progressRing) {
        progressRing.style.display = "none";
    }
}
// Sends a request to the local API with the specified route
function sendRequestToLocalAPI(route, isFile = false) {
    const apiUrl = `https://localhost:7225/PullRequest/${route}`;
    console.log("Sending request to:", apiUrl);
    disableDevOpsButtons();
    showProgressRing();
    const requestData = {
        OrganizationName: organizationName,
        RepositoryName: repositoryName,
        ProjectName: projectName,
        Id: prId,
        Query: ""
    };
    console.log("Request data:", requestData);
    fetch(apiUrl, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(requestData)
    })
        .then(response => {
        if (!response.ok) {
            throw new Error(`HTTP error: ${response.status}`);
        }
        return isFile ? response.blob() : response.json();
    })
        .then(data => {
        if (isFile) {
            const url = window.URL.createObjectURL(data);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${prId}.diff.txt`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);
        }
        else {
            const responseText = document.getElementById("responseText");
            if (responseText) {
                responseText.value = JSON.stringify(data, null, 2);
            }
        }
    })
        .catch(error => {
        console.error("Error sending request:", error);
        alert("Failed to retrieve data. Check if the server is running.");
    })
        .finally(() => {
        enableDevOpsButtons();
        hideProgressRing();
    });
}
// // Sends a request to review a specific file in the PR
// function reviewFile() {
//     if (!currentFile) {
//         alert("No file is currently selected.");
//         return;
//     }
//     sendRequestToLocalAPI(`ReviewFile`);
// }
// function observeFileChanges() {
//     const targetNode = document.body;
//     const config = { childList: true, subtree: true };
//     const observer = new MutationObserver(() => {
//         const newFile = detectCurrentFile();
//         if (newFile && newFile !== currentFile) {
//             currentFile = newFile;
//             enableReviewFileButton();
//             console.log("Now viewing file:", currentFile);
//         }
//         else if(!newFile)
//         {
//             disableReviewFileButton();
//         }
//     });
//     observer.observe(targetNode, config);
// }
// function detectCurrentFile(): string | null {
//     const url = window.location.href;
//     // Check if dev.azure.com and /pullrequest/ exist in the URL
//     if (url.includes("dev.azure.com") && url.includes("/pullrequest/")) {
//         const match = url.match(/path=([^&]+)/);
//         return match ? decodeURIComponent(match[1]) : null;
//     }
//     return null;
// }
// function enableReviewFileButton() {
//     const btn = document.getElementById("reviewFile") as HTMLButtonElement;
//     if (btn) {
//         btn.disabled = false;
//         btn.style.opacity = "1";
//         btn.style.cursor = "pointer";
//     }
// }
// function disableReviewFileButton() {
//     const btn = document.getElementById("reviewFile") as HTMLButtonElement;
//     if (btn) {
//         btn.disabled = true;
//         btn.style.opacity = "0.5";
//         btn.style.cursor = "not-allowed";
//     }
// }
