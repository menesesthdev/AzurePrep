// Lógica da tela de prova (SPA-like): navegação sem recarregar, timer com submissão
// automática, gravação incremental de respostas e sincronização do painel lateral.
(function () {
    "use strict";

    const data = JSON.parse(document.getElementById("exam-data").textContent);
    const total = data.totalQuestions;

    // Status por questão (index 0 = questão 1). Inicia com o que veio do servidor.
    const statuses = new Array(total);
    data.statuses.forEach(function (s) {
        statuses[s.number - 1] = { answered: s.answered, flagged: s.flagged };
    });

    let currentNumber = 1;
    let remaining = data.remainingSeconds;
    let questionEnteredAt = Date.now();
    let finished = false;

    const container = document.getElementById("question-container");
    const timerEl = document.getElementById("timer");
    const flagToggle = document.getElementById("flag-toggle");
    const navGrid = document.getElementById("nav-grid");

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

    // Grava a resposta da questão atual (seleção + flag + tempo gasto no intervalo).
    async function saveCurrent() {
        const { questionId, selected } = collectSelection();
        if (!questionId) return;

        const idx = currentNumber - 1;
        const flagged = statuses[idx] ? statuses[idx].flagged : false;
        const spent = Math.max(0, Math.round((Date.now() - questionEnteredAt) / 1000));
        questionEnteredAt = Date.now();

        statuses[idx] = { answered: selected.length > 0, flagged: flagged };
        renderNav();

        await postAnswer({
            questionId: questionId,
            selectedOptionIds: selected,
            isFlaggedForReview: flagged,
            timeSpentSeconds: spent
        });
    }

    // ---- Navegação -------------------------------------------------------
    async function goTo(n) {
        if (finished || n < 1 || n > total || n === currentNumber) return;
        await saveCurrent();

        const resp = await fetch(questionUrl(n), { credentials: "same-origin" });
        if (!resp.ok) return;
        container.innerHTML = await resp.text();

        currentNumber = n;
        questionEnteredAt = Date.now();
        syncFlagToggle();
        renderNav();
        window.scrollTo(0, 0);
    }

    function syncFlagToggle() {
        const idx = currentNumber - 1;
        flagToggle.checked = statuses[idx] ? statuses[idx].flagged : false;
    }

    // ---- Painel lateral --------------------------------------------------
    function renderNav() {
        navGrid.querySelectorAll(".nav-cell").forEach(function (cell) {
            const n = parseInt(cell.getAttribute("data-number"), 10);
            const st = statuses[n - 1] || { answered: false, flagged: false };
            cell.classList.toggle("nav-cell--answered", st.answered && !st.flagged);
            cell.classList.toggle("nav-cell--flagged", st.flagged);
            cell.classList.toggle("nav-cell--current", n === currentNumber);
        });
    }

    // ---- Timer -----------------------------------------------------------
    function formatTime(sec) {
        const s = Math.max(0, sec);
        const h = Math.floor(s / 3600);
        const m = Math.floor((s % 3600) / 60);
        const ss = s % 60;
        const pad = function (v) { return String(v).padStart(2, "0"); };
        return h > 0 ? `${pad(h)}:${pad(m)}:${pad(ss)}` : `${pad(m)}:${pad(ss)}`;
    }

    function tickTimer() {
        timerEl.textContent = formatTime(remaining);
        timerEl.classList.toggle("exam-header__timer--warning", remaining <= 300);
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

    // ---- Modal de confirmação -------------------------------------------
    const modal = document.getElementById("finish-modal");
    const summaryEl = document.getElementById("finish-summary");

    function openFinishModal() {
        saveCurrent();
        const answered = statuses.filter(function (s) { return s && s.answered; }).length;
        const flagged = statuses.filter(function (s) { return s && s.flagged; }).length;
        const unanswered = total - answered;
        summaryEl.textContent =
            `Respondidas: ${answered} de ${total}. ` +
            `Sem resposta: ${unanswered}. Marcadas para revisão: ${flagged}.`;
        modal.hidden = false;
    }

    function closeFinishModal() { modal.hidden = true; }

    // ---- Ligações de eventos --------------------------------------------
    container.addEventListener("change", function (e) {
        if (e.target.classList.contains("option__input")) {
            saveCurrent();
        }
    });

    flagToggle.addEventListener("change", function () {
        const idx = currentNumber - 1;
        const answered = statuses[idx] ? statuses[idx].answered : false;
        statuses[idx] = { answered: answered, flagged: flagToggle.checked };
        renderNav();
        saveCurrent();
    });

    document.getElementById("btn-prev").addEventListener("click", function () { goTo(currentNumber - 1); });
    document.getElementById("btn-next").addEventListener("click", function () { goTo(currentNumber + 1); });

    navGrid.addEventListener("click", function (e) {
        const cell = e.target.closest(".nav-cell");
        if (cell) { goTo(parseInt(cell.getAttribute("data-number"), 10)); }
    });

    document.getElementById("btn-review").addEventListener("click", openFinishModal);
    document.getElementById("btn-finish-top").addEventListener("click", openFinishModal);
    document.getElementById("btn-finish-cancel").addEventListener("click", closeFinishModal);
    document.getElementById("btn-finish-confirm").addEventListener("click", function () { doFinish(false); });

    // ---- Início ----------------------------------------------------------
    syncFlagToggle();
    renderNav();
    tickTimer();
    setInterval(tickTimer, 1000);
})();
