// Public visits only; no business actions, identities or automatic tracker listeners.
(() => {
    const config = document.currentScript;
    const key = 'staywidget.analytics-choice.v1';
    const titles = { '/': 'StayWidget', '/Pricing': 'StayWidget pricing', '/Demo': 'StayWidget demo', '/Install': 'StayWidget installation', '/Limitations': 'StayWidget limitations' };
    const path = config?.dataset.path;
    const origin = config?.dataset.origin;
    const website = config?.dataset.website;
    let disabled = false;
    const dnt = () => ['1', 'yes'].includes(navigator.doNotTrack) || window.doNotTrack === '1' || navigator.globalPrivacyControl === true;
    const optedOut = () => {
        if (disabled) return true;
        try {
            const raw = localStorage.getItem(key);
            if (!raw) return false;
            const choice = JSON.parse(raw);
            if (typeof choice.expiresAt !== 'number') return true;
            if (choice.expiresAt <= Date.now()) { localStorage.removeItem(key); return false; }
            return choice.disabled !== false;
        } catch { return true; }
    };
    const allowed = () => website && Object.hasOwn(titles, path) && location.pathname === path &&
        location.origin === origin && location.protocol === 'https:' && !dnt() && !optedOut();
    const checkbox = document.getElementById('analytics-opt-out');
    const save = document.getElementById('analytics-save');
    const status = document.getElementById('analytics-status');
    const refresh = () => {
        if (!checkbox || !save || !status) return;
        checkbox.checked = optedOut() || dnt(); checkbox.disabled = dnt(); save.disabled = dnt();
        status.textContent = dnt() ? 'Your browser privacy signal is enabled. Visit tracking is off.' : 'Your choice applies to this browser on this website.';
    };
    refresh();
    window.addEventListener('storage', refresh);
    save?.addEventListener('click', () => {
        disabled = checkbox.checked;
        try {
            localStorage.setItem(key, JSON.stringify({ disabled, expiresAt: Date.now() + 180 * 86400000 }));
            status.textContent = 'Preference saved for 180 days. It applies immediately; changing it does not send earlier visits.';
        } catch { disabled = true; status.textContent = 'Tracking is off on this page. Your browser could not save the choice; use Do Not Track for future visits.'; }
    });
    const payload = () => ({ website, hostname: new URL(origin).hostname, url: path, title: titles[path], referrer: '', language: navigator.language, screen: `${screen.width}x${screen.height}` });
    window.staywidgetUmamiBeforeSend = (type, incoming) => allowed() && type === 'event' && incoming?.name === undefined ? payload() : false;
    if (!allowed()) return;
    const script = document.createElement('script');
    script.src = 'https://cloud.umami.is/script.js'; script.async = true; script.referrerPolicy = 'no-referrer';
    script.dataset.websiteId = website; script.dataset.hostUrl = 'https://gateway.umami.is';
    script.dataset.autoTrack = 'false'; script.dataset.beforeSend = 'staywidgetUmamiBeforeSend';
    script.dataset.doNotTrack = 'true'; script.dataset.excludeSearch = 'true'; script.dataset.excludeHash = 'true';
    script.dataset.domains = new URL(origin).hostname;
    script.addEventListener('load', () => {
        if (!allowed() || !window.umami) return;
        try { void Promise.resolve(window.umami.track(payload())).catch(() => {}); } catch { /* Independent of product navigation. */ }
    }, { once: true });
    document.head.append(script);
})();
