// Lógica da tela de prova, fiel à entrega real (Pearson VUE / Microsoft):
// navegação linear sem recarregar a página, tela de revisão como único meio de saltar
// entre itens, timer com submissão automática e gravação incremental das respostas.
(function () {
    "use strict";

    const data = JSON.parse(document.getElementById("exam-data").textContent);
    const total = data.totalQuestions;

    // Status por item (index 0 = item 1). "seen" não vem do servidor: um item respondido
    // obrigatoriamente foi visto, e o item 1 é visto assim que a prova abre.
    const statuses = new Array(total);
    data.statuses.forEach(function (s) {
        statuses[s.number - 1] = {
            answered: s.answered,
            flagged: s.flagged,
            selectedCount: s.selectedCount,
            required: s.required,
            seen: s.answered
        };
    });

    // Comentários por item — a prova real permite anotar feedback sobre a questão.
    const comments = new Map();

    let currentNumber = 1;
    let remaining = data.remainingSeconds;
    let questionEnteredAt = Date.now();
    let finished = false;
    let reviewFilter = "all";
    let inReview = false;

    const container = document.getElementById("question-container");
    const questionView = document.getElementById("question-view");
    const reviewView = document.getElementById("review-view");
    const timerEl = document.getElementById("timer");
    const flagToggle = document.getElementById("flag-toggle");
    const reviewRows = document.getElementById("review-rows");
    const reviewEmpty = document.getElementById("review-empty");

    const btnPrev = document.getElementById("btn-prev");
    const btnNext = document.getElementById("btn-next");
    const btnReview = document.getElementById("btn-review");
    const btnEnd = document.getElementById("btn-end");
    const btnComments = document.getElementById("btn-comments");
    const markToggleEl = flagToggle.closest(".mark-toggle");

    // ---- Helpers de rede -------------------------------------------------
    function questionUrl(n) { return data.urls.question.replace("{n}", n); }

    async function postAnswer(payload) {
        await fetch(data.urls.answer, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "same-origin",
            body: JSON.stringify(payload)
        });
    }

    // ---- Estado ----------------------------------------------------------
    function statusOf(n) {
        return statuses[n - 1] || { answered: false, flagged: false, selectedCount: 0, required: 1, seen: false };
    }

    // Classificação idêntica à da tela de revisão real: um item de múltipla resposta com
    // seleção parcial é "Incompleto", não "Completo".
    function classify(n) {
        const st = statusOf(n);
        if (st.selectedCount >= st.required && st.selectedCount > 0) return "complete";
        if (st.seen || st.selectedCount > 0) return "incomplete";
        return "unseen";
    }

    const CLASSIFICATION_LABELS = {
        complete: "Completo",
        incomplete: "Incompleto",
        unseen: "Não visto"
    };

    // ---- Leitura do DOM da questão atual ---------------------------------
    function currentQuestionEl() { return container.querySelector(".question"); }

    function collectSelection() {
        const el = currentQuestionEl();
        if (!el) return { questionId: null, selected: [] };
        const inputs = el.querySelectorAll(".option__input:checked");
        return {
            questionId: el.getAttribute("data-question-id"),
            selected: Array.from(inputs).map(function (i) { return i.value; })
        };
    }

    // Grava a resposta do item atual (seleção + marcação + tempo gasto no intervalo).
    async function saveCurrent() {
        const selection = collectSelection();
        if (!selection.questionId) return;

        const idx = currentNumber - 1;
        const st = statusOf(currentNumber);
        const spent = Math.max(0, Math.round((Date.now() - questionEnteredAt) / 1000));
        questionEnteredAt = Date.now();

        statuses[idx] = {
            answered: selection.selected.length > 0,
            flagged: st.flagged,
            selectedCount: selection.selected.length,
            required: st.required,
            seen: true
        };

        await postAnswer({
            questionId: selection.questionId,
            selectedOptionIds: selection.selected,
            isFlaggedForReview: st.flagged,
            timeSpentSeconds: spent
        });
    }

    // ---- Navegação -------------------------------------------------------
    async function goTo(n) {
        if (finished || n < 1 || n > total) return;
        await saveCurrent();

        const resp = await fetch(questionUrl(n), { credentials: "same-origin" });
        if (!resp.ok) return;
        container.innerHTML = await resp.text();

        currentNumber = n;
        questionEnteredAt = Date.now();

        // Sincroniza "required" com o que o servidor devolveu para este item.
        const el = currentQuestionEl();
        const st = statusOf(n);
        st.required = parseInt(el.getAttribute("data-required"), 10) || 1;
        st.seen = true;
        statuses[n - 1] = st;

        showQuestionView();
        window.scrollTo(0, 0);
    }

    // ---- Alternância entre questão e tela de revisão ---------------------
    function showQuestionView() {
        inReview = false;
        questionView.hidden = false;
        reviewView.hidden = true;
        flagToggle.checked = statusOf(currentNumber).flagged;
        renderCommentsButton();
        renderFooter();
    }

    async function showReviewView() {
        await saveCurrent();
        inReview = true;
        questionView.hidden = true;
        reviewView.hidden = false;
        renderReviewRows();
        renderFooter();
        window.scrollTo(0, 0);
    }

    // A barra inferior muda conforme a tela — igual à prova real.
    function renderFooter() {
        markToggleEl.hidden = inReview;
        btnComments.hidden = inReview;

        if (inReview) {
            btnPrev.hidden = true;
            btnNext.hidden = true;
            btnReview.hidden = false;
            btnReview.textContent = "Voltar à prova";
            btnEnd.hidden = false;
            return;
        }

        btnReview.hidden = false;
        btnReview.textContent = "Tela de revisão";
        btnPrev.hidden = false;
        btnPrev.disabled = currentNumber === 1;

        // No último item, "Próxima" dá lugar a "Encerrar prova".
        const isLast = currentNumber === total;
        btnNext.hidden = isLast;
        btnEnd.hidden = !isLast;
    }

    function renderCommentsButton() {
        btnComments.classList.toggle("has-comment", comments.has(currentNumber));
    }

    // ---- Tela de revisão -------------------------------------------------
    function renderReviewRows() {
        reviewRows.innerHTML = "";
        let shown = 0;

        for (let n = 1; n <= total; n++) {
            const kind = classify(n);
            const st = statusOf(n);

            if (reviewFilter === "incomplete" && kind === "complete") continue;
            if (reviewFilter === "marked" && !st.flagged) continue;
            shown++;

            const tr = document.createElement("tr");
            tr.className = "review-row review-row--" + kind;
            tr.tabIndex = 0;
            tr.setAttribute("role", "button");
            tr.setAttribute("data-number", n);

            const tdItem = document.createElement("td");
            tdItem.className = "review-row__item";
            tdItem.textContent = n;

            const tdStatus = document.createElement("td");
            tdStatus.textContent = CLASSIFICATION_LABELS[kind];

            const tdMark = document.createElement("td");
            tdMark.className = "review-row__mark";
            tdMark.textContent = st.flagged ? "✔" : "";

            tr.append(tdItem, tdStatus, tdMark);
            reviewRows.appendChild(tr);
        }

        reviewEmpty.hidden = shown > 0;
    }

    // ---- Timer -----------------------------------------------------------
    function formatTime(sec) {
        const s = Math.max(0, sec);
        const h = Math.floor(s / 3600);
        const m = Math.floor((s % 3600) / 60);
        const ss = s % 60;
        const pad = function (v) { return String(v).padStart(2, "0"); };
        return `${pad(h)}:${pad(m)}:${pad(ss)}`;
    }

    function tickTimer() {
        timerEl.textContent = formatTime(remaining);
        timerEl.classList.toggle("is-low", remaining <= 300);
        if (remaining <= 0 && !finished) {
            // Tempo zerado = submissão automática, sem exceção.
            doFinish(true);
            return;
        }
        remaining -= 1;
    }

    // ---- Finalização -----------------------------------------------------
    async function doFinish(auto) {
        if (finished) return;
        finished = true;
        if (!auto) {
            await saveCurrent();
        }
        const resp = await fetch(data.urls.finish, {
            method: "POST",
            credentials: "same-origin"
        });
        const json = await resp.json();
        window.location.href = json.redirectUrl;
    }

    // ---- Modais ----------------------------------------------------------
    const finishModal = document.getElementById("finish-modal");
    const finishSummary = document.getElementById("finish-summary");
    const commentsModal = document.getElementById("comments-modal");
    const commentsText = document.getElementById("comments-text");

    async function openFinishModal() {
        await saveCurrent();

        let complete = 0, incomplete = 0, unseen = 0, marked = 0;
        for (let n = 1; n <= total; n++) {
            const kind = classify(n);
            if (kind === "complete") complete++;
            else if (kind === "incomplete") incomplete++;
            else unseen++;
            if (statusOf(n).flagged) marked++;
        }

        finishSummary.textContent =
            `Completos: ${complete} de ${total}. ` +
            `Incompletos: ${incomplete}. Não vistos: ${unseen}. Marcados: ${marked}.`;
        finishModal.hidden = false;
    }

    function openCommentsModal() {
        commentsText.value = comments.get(currentNumber) || "";
        commentsModal.hidden = false;
        commentsText.focus();
    }

    // ---- Ligações de eventos --------------------------------------------
    container.addEventListener("change", function (e) {
        if (e.target.classList.contains("option__input")) {
            saveCurrent();
        }
    });

    flagToggle.addEventListener("change", function () {
        const st = statusOf(currentNumber);
        st.flagged = flagToggle.checked;
        statuses[currentNumber - 1] = st;
        saveCurrent();
    });

    btnPrev.addEventListener("click", function () { goTo(currentNumber - 1); });
    btnNext.addEventListener("click", function () { goTo(currentNumber + 1); });
    btnEnd.addEventListener("click", openFinishModal);

    btnReview.addEventListener("click", function () {
        if (inReview) { showQuestionView(); } else { showReviewView(); }
    });

    reviewView.querySelectorAll(".btn--filter").forEach(function (btn) {
        btn.addEventListener("click", function () {
            reviewFilter = btn.getAttribute("data-filter");
            reviewView.querySelectorAll(".btn--filter").forEach(function (b) {
                b.classList.toggle("is-active", b === btn);
            });
            renderReviewRows();
        });
    });

    function jumpFromReview(e) {
        const row = e.target.closest(".review-row");
        if (!row) return;
        goTo(parseInt(row.getAttribute("data-number"), 10));
    }

    reviewRows.addEventListener("click", jumpFromReview);
    reviewRows.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            jumpFromReview(e);
        }
    });

    btnComments.addEventListener("click", openCommentsModal);
    document.getElementById("btn-comments-cancel").addEventListener("click", function () {
        commentsModal.hidden = true;
    });
    document.getElementById("btn-comments-save").addEventListener("click", function () {
        const text = commentsText.value.trim();
        if (text) { comments.set(currentNumber, text); } else { comments.delete(currentNumber); }
        commentsModal.hidden = true;
        renderCommentsButton();
    });

    document.getElementById("btn-finish-cancel").addEventListener("click", function () {
        finishModal.hidden = true;
    });
    document.getElementById("btn-finish-confirm").addEventListener("click", function () {
        doFinish(false);
    });

    // ---- Início ----------------------------------------------------------
    const firstEl = currentQuestionEl();
    if (firstEl) {
        const st = statusOf(1);
        st.required = parseInt(firstEl.getAttribute("data-required"), 10) || 1;
        st.seen = true;
        statuses[0] = st;
    }

    showQuestionView();
    tickTimer();
    setInterval(tickTimer, 1000);
})();
