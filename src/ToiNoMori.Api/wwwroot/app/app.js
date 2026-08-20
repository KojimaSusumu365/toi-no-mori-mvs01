"use strict";

const elements = {
  networkStatus: document.querySelector("#network-status"),
  searchForm: document.querySelector("#search-form"),
  query: document.querySelector("#query"),
  tag: document.querySelector("#tag"),
  searchStatus: document.querySelector("#search-status"),
  questionList: document.querySelector("#question-list"),
  clearSearch: document.querySelector("#clear-search"),
  signInLink: document.querySelector("#sign-in-link"),
  signInHelp: document.querySelector("#sign-in-help"),
  signedOutPanel: document.querySelector("#signed-out-panel"),
  signedInPanel: document.querySelector("#signed-in-panel"),
  sessionName: document.querySelector("#session-name"),
  sessionRoles: document.querySelector("#session-roles"),
  logoutCsrf: document.querySelector("#logout-csrf"),
  createForm: document.querySelector("#create-form"),
  editorFormHeading: document.querySelector("#editor-form-heading"),
  editorSubmit: document.querySelector("#editor-submit"),
  cancelEdit: document.querySelector("#cancel-edit"),
  editorStatus: document.querySelector("#editor-status"),
  editorQuestionList: document.querySelector("#editor-question-list"),
  reviewerStatus: document.querySelector("#reviewer-status"),
  reviewQueueList: document.querySelector("#review-queue-list"),
  publishedQuestionList: document.querySelector("#published-question-list"),
  auditStatus: document.querySelector("#audit-status"),
  auditList: document.querySelector("#audit-list"),
  refreshEditor: document.querySelector("#refresh-editor"),
  refreshReviewer: document.querySelector("#refresh-reviewer"),
  refreshAudit: document.querySelector("#refresh-audit"),
  workspaceTabs: Array.from(document.querySelectorAll(".workspace-tab")),
  workspaceViews: Array.from(document.querySelectorAll(".workspace-view"))
};

const statusLabels = {
  DRAFT: "下書き",
  IN_REVIEW: "レビュー待ち",
  PUBLISHED: "公開中",
  WITHDRAWN: "取り下げ"
};

const state = {
  csrfToken: "",
  session: null,
  managedQuestions: [],
  editingQuestion: null,
  activeSearch: null
};

function setStatus(element, message, isError = false) {
  element.textContent = message;
  element.classList.toggle("error", isError);
}

function updateNetworkStatus() {
  const online = navigator.onLine;
  elements.networkStatus.textContent = online ? "オンライン" : "オフライン";
  elements.networkStatus.classList.toggle("offline", !online);
}

async function fetchJson(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    credentials: "same-origin",
    headers: {
      Accept: "application/json",
      ...(options.headers || {})
    }
  });

  if (!response.ok) {
    let problem;
    try {
      problem = await response.json();
    } catch {
      problem = null;
    }
    const error = new Error(String(problem?.title || `Request failed with status ${response.status}`));
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

function csrfHeaders(extra = {}) {
  return {
    "X-CSRF-Token": state.csrfToken,
    ...extra
  };
}

function addTextElement(parent, tagName, className, text) {
  const element = document.createElement(tagName);
  if (className) {
    element.className = className;
  }
  element.textContent = text;
  parent.append(element);
  return element;
}

function addActionButton(parent, label, className, action, disabled = false) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = className;
  button.textContent = label;
  button.disabled = disabled;
  button.addEventListener("click", action);
  parent.append(button);
  return button;
}

function renderTags(parent, values) {
  const tags = document.createElement("ul");
  tags.className = "tag-list";
  tags.setAttribute("aria-label", "タグ");
  for (const tag of Array.isArray(values) ? values : []) {
    addTextElement(tags, "li", "", String(tag));
  }
  parent.append(tags);
}

function renderQuestions(questions) {
  elements.questionList.replaceChildren();
  if (questions.length === 0) {
    const item = document.createElement("li");
    item.className = "question-card";
    addTextElement(item, "h3", "", "該当する問いはありません");
    addTextElement(item, "p", "muted", "条件を変えて、もう一度探してください。");
    elements.questionList.append(item);
    return;
  }

  const dateFormatter = new Intl.DateTimeFormat("ja-JP", { dateStyle: "medium" });
  for (const question of questions) {
    const item = document.createElement("li");
    item.className = "question-card";
    addTextElement(item, "h3", "", String(question.title || "名称未設定"));
    addTextElement(item, "p", "", String(question.body || ""));
    addTextElement(item, "p", "question-meta", `公開日 ${dateFormatter.format(new Date(question.publishedAt))}`);
    renderTags(item, question.tags);
    elements.questionList.append(item);
  }
}

async function searchQuestions() {
  if (state.activeSearch) {
    state.activeSearch.abort();
  }
  state.activeSearch = new AbortController();
  const parameters = new URLSearchParams();
  const query = elements.query.value.trim();
  const tag = elements.tag.value.trim();
  if (query) {
    parameters.set("query", query);
  }
  if (tag) {
    parameters.set("tag", tag);
  }
  parameters.set("limit", "20");

  setStatus(elements.searchStatus, "問いを検索しています。");
  try {
    const questions = await fetchJson(`/api/public/questions?${parameters}`, {
      signal: state.activeSearch.signal
    });
    renderQuestions(Array.isArray(questions) ? questions : []);
    setStatus(elements.searchStatus, `${Array.isArray(questions) ? questions.length : 0}件の問いを表示しています。`);
  } catch (error) {
    if (error.name !== "AbortError") {
      renderQuestions([]);
      setStatus(elements.searchStatus, "問いを取得できませんでした。しばらくしてから再度お試しください。", true);
    }
  }
}

function hasRole(role) {
  return Array.isArray(state.session?.roles) && state.session.roles.includes(role);
}

function activateView(viewId) {
  for (const view of elements.workspaceViews) {
    view.hidden = view.id !== viewId;
  }
  for (const tab of elements.workspaceTabs) {
    tab.setAttribute("aria-pressed", String(tab.dataset.view === viewId));
  }
  if (viewId === "audit-view") {
    loadAudit();
  }
}

function configureWorkspaceRoles() {
  for (const role of state.session.roles) {
    const badge = document.createElement("span");
    badge.className = "role-badge";
    badge.textContent = role === "Editor" ? "編集担当" : role === "Reviewer" ? "審査担当" : role;
    elements.sessionRoles.append(badge);
  }

  const editorTab = document.querySelector("#editor-view-button");
  const reviewerTab = document.querySelector("#reviewer-view-button");
  const auditTab = document.querySelector("#audit-view-button");
  editorTab.hidden = !hasRole("Editor");
  reviewerTab.hidden = !hasRole("Reviewer");
  auditTab.hidden = !hasRole("Reviewer");
  activateView(hasRole("Editor") ? "editor-view" : "reviewer-view");
}

async function loadSession() {
  try {
    const config = await fetchJson("/bff/config");
    if (!config.signInEnabled) {
      elements.signInLink.hidden = true;
      elements.signInHelp.textContent = "現在、管理者ログインは準備中です。公開された問いは閲覧できます。";
    }
  } catch {
    elements.signInHelp.textContent = "ログイン設定を確認できませんでした。";
  }

  try {
    const session = await fetchJson("/bff/session");
    state.session = session;
    state.csrfToken = String(session.csrfToken || "");
    elements.logoutCsrf.value = state.csrfToken;
    elements.sessionName.textContent = session.displayName || session.subject || "担当者";
    elements.signedOutPanel.hidden = true;
    elements.signedInPanel.hidden = false;
    elements.signInLink.hidden = true;
    elements.sessionRoles.replaceChildren();
    configureWorkspaceRoles();
    await loadManagedQuestions();
  } catch (error) {
    if (error.status !== 401 && error.status !== 403) {
      elements.signInHelp.textContent = "セッションを確認できませんでした。";
    }
  }
}

function parseTags(value) {
  return value.split(",").map(tag => tag.trim()).filter(Boolean).slice(0, 5);
}

function formPayload() {
  const form = new FormData(elements.createForm);
  return {
    title: String(form.get("title") || ""),
    body: String(form.get("body") || ""),
    tags: parseTags(String(form.get("tags") || ""))
  };
}

function resetEditor() {
  state.editingQuestion = null;
  elements.createForm.reset();
  elements.editorFormHeading.textContent = "新しい問いを下書きにする";
  elements.editorSubmit.textContent = "下書きを保存";
  elements.cancelEdit.hidden = true;
}

function beginEdit(question) {
  state.editingQuestion = question;
  elements.createForm.elements.title.value = question.title;
  elements.createForm.elements.body.value = question.body;
  elements.createForm.elements.tags.value = question.tags.join(", ");
  elements.editorFormHeading.textContent = "下書きを編集する";
  elements.editorSubmit.textContent = "変更を保存";
  elements.cancelEdit.hidden = false;
  elements.createForm.scrollIntoView({ behavior: "smooth", block: "start" });
  elements.createForm.elements.title.focus({ preventScroll: true });
}

function operationError(error, fallback) {
  if (error.status === 401) {
    return "セッションが終了しました。再ログインしてください。";
  }
  if (error.status === 403) {
    return "この操作を行う権限がないか、安全確認に失敗しました。";
  }
  if (error.status === 409) {
    return "他の操作で内容が更新されました。再読込して確認してください。";
  }
  return fallback;
}

async function saveDraft(event) {
  event.preventDefault();
  if (!elements.createForm.reportValidity()) {
    return;
  }
  if (!state.csrfToken) {
    setStatus(elements.editorStatus, "安全確認用トークンがありません。再ログインしてください。", true);
    return;
  }

  const editing = state.editingQuestion;
  setStatus(elements.editorStatus, editing ? "変更を保存しています。" : "下書きを保存しています。");
  try {
    if (editing) {
      await fetchJson(`/api/admin/questions/${editing.id}`, {
        method: "PUT",
        headers: csrfHeaders({
          "Content-Type": "application/json",
          "If-Match": `"${editing.version}"`
        }),
        body: JSON.stringify(formPayload())
      });
    } else {
      await fetchJson("/api/admin/questions", {
        method: "POST",
        headers: csrfHeaders({ "Content-Type": "application/json" }),
        body: JSON.stringify(formPayload())
      });
    }
    resetEditor();
    await loadManagedQuestions();
    setStatus(elements.editorStatus, editing ? "変更を保存しました。" : "下書きを保存しました。");
  } catch (error) {
    setStatus(elements.editorStatus, operationError(error, "保存できませんでした。入力内容を確認してください。"), true);
  }
}

async function submitQuestion(question) {
  setStatus(elements.editorStatus, "レビューを申請しています。");
  try {
    await fetchJson(`/api/admin/questions/${question.id}/submit`, {
      method: "POST",
      headers: csrfHeaders()
    });
    await loadManagedQuestions();
    setStatus(elements.editorStatus, "レビューを申請しました。");
  } catch (error) {
    setStatus(elements.editorStatus, operationError(error, "レビューを申請できませんでした。"), true);
  }
}

function addQuestionSummary(item, question, includeOwner = false) {
  const heading = document.createElement("div");
  heading.className = "management-heading";
  addTextElement(heading, "h4", "", String(question.title || "名称未設定"));
  addTextElement(heading, "span", `status-badge status-${String(question.status).toLowerCase()}`, statusLabels[question.status] || question.status);
  item.append(heading);
  addTextElement(item, "p", "management-body", String(question.body || ""));
  if (includeOwner) {
    addTextElement(item, "p", "question-meta", `作成者 ${question.ownerSubject}`);
  }
  addTextElement(item, "p", "question-meta", `版 ${question.version}・更新 ${new Date(question.updatedAt).toLocaleString("ja-JP")}`);
  if (question.reviewReason) {
    addTextElement(item, "p", "review-reason", `理由：${question.reviewReason}`);
  }
  renderTags(item, question.tags);
}

function renderEmptyList(list, message) {
  list.replaceChildren();
  const item = document.createElement("li");
  item.className = "management-card empty-card";
  item.textContent = message;
  list.append(item);
}

function renderEditorQuestions() {
  const questions = state.managedQuestions.filter(question => question.ownerSubject === state.session.subject);
  if (questions.length === 0) {
    renderEmptyList(elements.editorQuestionList, "まだ問いはありません。上のフォームから最初の問いを作成できます。");
    return;
  }

  elements.editorQuestionList.replaceChildren();
  for (const question of questions) {
    const item = document.createElement("li");
    item.className = "management-card";
    addQuestionSummary(item, question);
    if (question.status === "DRAFT") {
      const actions = document.createElement("div");
      actions.className = "action-row";
      addActionButton(actions, "編集", "button button-quiet", () => beginEdit(question));
      addActionButton(actions, "レビューを申請", "button button-primary", () => submitQuestion(question));
      item.append(actions);
    }
    elements.editorQuestionList.append(item);
  }
}

async function reviewQuestion(question, action, reason = "") {
  setStatus(elements.reviewerStatus, action === "approve" ? "承認しています。" : "処理しています。");
  const headers = csrfHeaders();
  let body;
  if (action === "approve") {
    headers["Idempotency-Key"] = crypto.randomUUID();
    headers["If-Match"] = `"${question.version}"`;
  } else {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify({ reason });
  }

  try {
    await fetchJson(`/api/admin/questions/${question.id}/${action}`, {
      method: "POST",
      headers,
      body
    });
    await loadManagedQuestions();
    await searchQuestions();
    setStatus(elements.reviewerStatus, action === "approve" ? "承認し、公開しました。" : "処理を完了しました。");
  } catch (error) {
    setStatus(elements.reviewerStatus, operationError(error, "処理できませんでした。理由と状態を確認してください。"), true);
  }
}

function reasonControl(item, question, action, labelText, buttonText) {
  const field = document.createElement("div");
  field.className = "field compact-field";
  const id = `${action}-reason-${question.id}`;
  const label = document.createElement("label");
  label.htmlFor = id;
  label.textContent = labelText;
  const textarea = document.createElement("textarea");
  textarea.id = id;
  textarea.rows = 2;
  textarea.maxLength = 1000;
  textarea.required = true;
  field.append(label, textarea);
  item.append(field);
  addActionButton(item, buttonText, "button button-quiet", () => {
    const reason = textarea.value.trim();
    if (!reason) {
      textarea.setCustomValidity("理由を入力してください。");
      textarea.reportValidity();
      textarea.setCustomValidity("");
      return;
    }
    reviewQuestion(question, action, reason);
  });
}

function renderReviewerQuestions() {
  const queue = state.managedQuestions.filter(question => question.status === "IN_REVIEW");
  const published = state.managedQuestions.filter(question => question.status === "PUBLISHED");

  if (queue.length === 0) {
    renderEmptyList(elements.reviewQueueList, "現在、レビュー待ちの問いはありません。");
  } else {
    elements.reviewQueueList.replaceChildren();
    for (const question of queue) {
      const item = document.createElement("li");
      item.className = "management-card";
      addQuestionSummary(item, question, true);
      reasonControl(item, question, "return", "差し戻し理由", "差し戻す");
      const selfApproval = question.ownerSubject === state.session.subject;
      addActionButton(item, "承認して公開", "button button-primary", () => reviewQuestion(question, "approve"), selfApproval);
      if (selfApproval) {
        addTextElement(item, "p", "muted", "自分が作成した問いは承認できません。");
      }
      elements.reviewQueueList.append(item);
    }
  }

  if (published.length === 0) {
    renderEmptyList(elements.publishedQuestionList, "公開中の問いはありません。");
  } else {
    elements.publishedQuestionList.replaceChildren();
    for (const question of published) {
      const item = document.createElement("li");
      item.className = "management-card";
      addQuestionSummary(item, question, true);
      reasonControl(item, question, "withdraw", "取り下げ理由", "公開を取り下げる");
      elements.publishedQuestionList.append(item);
    }
  }
}

async function loadManagedQuestions() {
  if (!state.session) {
    return;
  }
  setStatus(elements.editorStatus, "一覧を読み込んでいます。");
  setStatus(elements.reviewerStatus, "一覧を読み込んでいます。");
  try {
    const questions = await fetchJson("/api/admin/questions?limit=100");
    state.managedQuestions = Array.isArray(questions) ? questions : [];
    if (hasRole("Editor")) {
      renderEditorQuestions();
    }
    if (hasRole("Reviewer")) {
      renderReviewerQuestions();
    }
    setStatus(elements.editorStatus, "");
    setStatus(elements.reviewerStatus, "");
  } catch (error) {
    const message = operationError(error, "管理対象の問いを取得できませんでした。");
    setStatus(elements.editorStatus, message, true);
    setStatus(elements.reviewerStatus, message, true);
  }
}

async function loadAudit() {
  if (!hasRole("Reviewer")) {
    return;
  }
  setStatus(elements.auditStatus, "監査履歴を読み込んでいます。");
  try {
    const records = await fetchJson("/api/admin/audit");
    elements.auditList.replaceChildren();
    for (const record of (Array.isArray(records) ? records : []).slice(-50).reverse()) {
      const item = document.createElement("li");
      item.className = "audit-card";
      addTextElement(item, "strong", "", `${record.action} — ${record.result}`);
      addTextElement(item, "span", "question-meta", `${new Date(record.occurredAt).toLocaleString("ja-JP")}・${record.actor}`);
      addTextElement(item, "span", "question-meta", `対象 ${record.targetId}・追跡 ${record.correlationId}`);
      elements.auditList.append(item);
    }
    setStatus(elements.auditStatus, `${Array.isArray(records) ? records.length : 0}件中、直近50件までを表示しています。`);
  } catch (error) {
    setStatus(elements.auditStatus, operationError(error, "監査履歴を取得できませんでした。"), true);
  }
}

elements.searchForm.addEventListener("submit", event => {
  event.preventDefault();
  searchQuestions();
});
elements.clearSearch.addEventListener("click", () => {
  elements.searchForm.reset();
  elements.query.focus();
  searchQuestions();
});
elements.createForm.addEventListener("submit", saveDraft);
elements.cancelEdit.addEventListener("click", () => {
  resetEditor();
  setStatus(elements.editorStatus, "編集を取り消しました。");
});
elements.refreshEditor.addEventListener("click", loadManagedQuestions);
elements.refreshReviewer.addEventListener("click", loadManagedQuestions);
elements.refreshAudit.addEventListener("click", loadAudit);
for (const tab of elements.workspaceTabs) {
  tab.addEventListener("click", () => activateView(tab.dataset.view));
}
window.addEventListener("online", () => {
  updateNetworkStatus();
  searchQuestions();
  loadManagedQuestions();
});
window.addEventListener("offline", updateNetworkStatus);

updateNetworkStatus();
loadSession();
searchQuestions();
