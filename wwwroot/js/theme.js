class ThemeManager {
    constructor() {
        this.storageKey = 'app_theme';
        this.defaultTheme = 'light';
        // Initialize immediately to prevent flash
        this.init();
    }

    init() {
        let stored = null;
        try {
            stored = localStorage.getItem(this.storageKey);
        } catch (e) {
            console.warn('Storage access blocked:', e);
        }
        const theme = stored || this.defaultTheme;
        if (this.currentTheme !== theme) {
            this.applyTheme(theme);
        }

        // Bind events if DOM is ready, or wait
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.setupUI());
        } else {
            this.setupUI();
        }
    }

    setupUI() {
        const toggles = document.querySelectorAll('.theme-toggle');
        toggles.forEach(btn => {
            btn.onclick = () => this.toggle();
            this.updateIcon(btn, this.currentTheme);
        });
    }

    get currentTheme() {
        return document.documentElement.getAttribute('data-theme') || this.defaultTheme;
    }

    toggle() {
        const current = this.currentTheme;
        const next = current === 'dark' ? 'light' : 'dark';
        this.applyTheme(next);
        this.saveTheme(next);
    }

    applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);

        // Update all buttons
        const toggles = document.querySelectorAll('.theme-toggle');
        toggles.forEach(btn => this.updateIcon(btn, theme));
    }

    saveTheme(theme) {
        try {
            localStorage.setItem(this.storageKey, theme);
        } catch (e) {
            console.warn('Could not save theme to storage:', e);
        }
    }

    updateIcon(btn, theme) {
        // Simple text/emoji based fallback, but we will use Bootstrap icons if available
        // Assuming we'll inject HTML for the button
        const isDark = theme === 'dark';
        btn.innerHTML = isDark ? '🌙' : '☀️';
        btn.setAttribute('title', isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode');
    }
}

window.ThemeManager = new ThemeManager();
