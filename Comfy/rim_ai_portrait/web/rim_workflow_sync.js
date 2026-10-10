import { app } from "../../scripts/app.js";
import { api } from "../../scripts/api.js";

let syncTimer;
let latestRevision = 0;

function requestNode() {
    const nodes = (app.graph && app.graph._nodes) || [];
    const requests = nodes.filter(node => node.comfyClass === "RimPortraitRequest"
        || node.type === "RimPortraitRequest");
    return requests.length === 1 ? requests[0] : null;
}

function modelName(node) {
    const widget = (node.widgets || []).find(item => item.name === "model");
    return widget && typeof widget.value === "string" ? widget.value.trim() : "";
}

function savedWorkflowName() {
    try {
        for (let index = 0; index < sessionStorage.length; index++) {
            const key = sessionStorage.key(index);
            if (!key?.startsWith("Comfy.Workflow.ActivePath:")) continue;
            const pointer = JSON.parse(sessionStorage.getItem(key));
            const filename = pointer?.path?.split(/[\\/]/).pop();
            if (filename?.toLowerCase().endsWith(".json"))
                return filename.slice(0, -5);
        }
    } catch (error) {
        console.warn("[Rim AI Portrait] Could not check the saved workflow name:", error);
    }
    return null;
}

async function showLastRequest(node) {
    const model = modelName(node);
    try {
        const response = await api.fetchApi(`/rim_portrait/last-request/${encodeURIComponent(model)}`);
        const raw = await response.text();
        let result;
        try { result = JSON.parse(raw); }
        catch { throw new Error(`HTTP ${response.status}: ${raw.slice(0, 160)}`); }
        if (!response.ok) throw new Error(result.error?.message || `HTTP ${response.status}`);
        if (!result.received) {
            window.alert(`No RimWorld request has been received for ${model} since ComfyUI started.`);
            return;
        }
        const dialog = document.createElement("dialog");
        dialog.style.cssText = "width:min(900px,90vw);max-height:85vh;overflow:auto;background:#222;color:white;border:1px solid #777;";
        const title = document.createElement("h3");
        title.textContent = `Last RimWorld input: ${model}`;
        dialog.append(title);
        const close = document.createElement("button");
        close.textContent = "Close";
        close.addEventListener("click", () => dialog.close());
        dialog.append(close);
        for (const [label, value] of [["Prompt", result.prompt], ["Options JSON", result.options]]) {
            const heading = document.createElement("h4");
            heading.textContent = label;
            dialog.append(heading);
            const content = document.createElement("pre");
            content.textContent = value || "(empty)";
            content.style.cssText = "white-space:pre-wrap;overflow-wrap:anywhere;max-height:240px;overflow:auto;padding:8px;background:#111;user-select:text;";
            dialog.append(content);
        }
        if (result.has_mask) {
            const maskNote = document.createElement("p");
            maskNote.textContent = "An inpaint mask was included.";
            dialog.append(maskNote);
        }
        const gallery = document.createElement("div");
        gallery.style.cssText = "display:flex;flex-wrap:wrap;gap:12px;margin-top:12px;";
        if (!result.images.length) {
            const note = document.createElement("p");
            note.textContent = "This request did not include an input image.";
            gallery.append(note);
        }
        result.images.forEach((encoded, index) => {
            const item = document.createElement("figure");
            item.style.margin = "0";
            const caption = document.createElement("figcaption");
            caption.textContent = index === 0 ? "Primary image" : `Reference image ${index}`;
            const image = document.createElement("img");
            image.src = `data:image/png;base64,${encoded}`;
            image.style.cssText = "display:block;max-width:320px;max-height:320px;object-fit:contain;";
            item.append(caption, image);
            gallery.append(item);
        });
        dialog.append(gallery);
        dialog.addEventListener("close", () => dialog.remove());
        document.body.append(dialog);
        dialog.showModal();
    } catch (error) {
        window.alert(`Could not load RimWorld input preview: ${error.message}`);
    }
}

async function syncCurrentWorkflow(reportErrors = false) {
    const revision = ++latestRevision;
    const node = requestNode();
    if (!node) return;
    const model = modelName(node);
    try {
        const savedName = savedWorkflowName();
        if (savedName && savedName !== model)
            throw new Error(`Saved workflow ${savedName}.json has request model ${model}. Set the node model to ${savedName} before syncing.`);
        const output = (await app.graphToPrompt()).output;
        if (revision !== latestRevision) return;
        const response = await api.fetchApi("/rim_portrait/workflows", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ model, workflow: output }),
        });
        if (!response.ok) {
            const result = await response.json();
            throw new Error(result.error?.message || `HTTP ${response.status}`);
        }
        if (reportErrors) window.alert(`RimWorld workflow synced: ${model}`);
    } catch (error) {
        if (reportErrors) window.alert(`RimWorld workflow sync failed: ${error.message}`);
        else console.warn("[Rim AI Portrait] Workflow sync failed:", error);
    }
}

function scheduleSync() {
    clearTimeout(syncTimer);
    const revision = ++latestRevision;
    syncTimer = setTimeout(() => {
        if (revision === latestRevision) void syncCurrentWorkflow();
    }, 1500);
}

app.registerExtension({
    name: "RimAIPortrait.WorkflowSync",
    nodeCreated(node) {
        if (node.comfyClass !== "RimPortraitRequest" && node.type !== "RimPortraitRequest") return;
        node.addWidget("button", "Preview RimWorld input", "", () => void showLastRequest(node),
            { serialize: false });
    },
    setup() {
        api.addEventListener("graphChanged", scheduleSync);
        api.addEventListener("promptQueued", () => void syncCurrentWorkflow());
        scheduleSync();
    },
    afterConfigureGraph() {
        scheduleSync();
    },
    getNodeMenuItems(node) {
        if (node.comfyClass !== "RimPortraitRequest" && node.type !== "RimPortraitRequest")
            return [];
        return [
            { content: "Preview RimWorld input", callback: () => void showLastRequest(node) },
            { content: "Sync workflow for RimWorld", callback: () => void syncCurrentWorkflow(true) },
        ];
    },
});
