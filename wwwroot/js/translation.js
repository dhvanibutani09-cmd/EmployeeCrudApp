class TranslationManager {
    constructor() {
        this.apiEndpoint = '/api/translation/translate';

        // 1. Storage or Browser Detection
        let storedLang = null;
        try {
            storedLang = localStorage.getItem('app_language');
        } catch (e) {
            console.warn('Storage access blocked for translations:', e);
        }
        const browserLang = navigator.language.split('-')[0];
        // Supported langs in our UI (expanded for regional support)
        const supported = ['en', 'hi', 'gu', 'mr', 'bn', 'ta', 'es', 'fr', 'de'];


        // Auto-detect: Use stored, or browser if supported, else default to 'en'
        this.currentLang = storedLang || (supported.includes(browserLang) ? browserLang : 'en');

        this.isTranslating = false;
        this.cache = new Map();
        this.cache.set('en', new Map());

        this.observer = null;
        this.debouncedTranslate = this.debounce(this.processQueue.bind(this), 300);
        this.mutationQueue = new Set(); // Stores text nodes
        this.attributeQueue = new Set(); // Stores elements with attributes

        const init = () => {
            this.setupDropdown();
            this.setupObserver();
            if (this.currentLang !== 'en') {
                this.translatePage();
            }
        };

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    setupDropdown() {
        const select = document.getElementById('language-selector');
        if (select) {
            select.value = this.currentLang;
            select.addEventListener('change', (e) => this.setLanguage(e.target.value));
        }
    }

    setupObserver() {
        this.observer = new MutationObserver((mutations) => {
            if (this.currentLang === 'en') return;

            let hasRelevantMutation = false;

            mutations.forEach(mutation => {
                if (mutation.type === 'childList') {
                    mutation.addedNodes.forEach(node => {
                        if (this.isValidNode(node)) {
                            // 1. Text Nodes
                            this.collectTextNodes(node).forEach(bgNode => this.mutationQueue.add(bgNode));

                            // 2. Attributes on added nodes
                            const attrSelector = 'input[type="submit"], input[type="button"], input[type="reset"], [placeholder], [title]';
                            if (node.matches && node.matches(attrSelector)) this.attributeQueue.add(node);
                            if (node.querySelectorAll) {
                                node.querySelectorAll(attrSelector).forEach(el => this.attributeQueue.add(el));
                            }
                            hasRelevantMutation = true;
                        }
                    });
                } else if (mutation.type === 'characterData') {
                    const node = mutation.target;
                    // Ignore if we are applying translation
                    if (!node._isApplyingTranslation && !node.parentElement?._isApplyingTranslation && this.isValidTextNode(node)) {
                        node._originalText = node.nodeValue;
                        this.mutationQueue.add(node);
                        hasRelevantMutation = true;
                    }
                } else if (mutation.type === 'attributes') {
                    const node = mutation.target;
                    // Ignore if we are applying translation
                    if (!node._isApplyingTranslation) {
                        const attr = mutation.attributeName;
                        if (['placeholder', 'title', 'value'].includes(attr)) {
                            this.attributeQueue.add(node);
                            hasRelevantMutation = true;
                        }
                    }
                }
            });

            if (hasRelevantMutation) {
                this.debouncedTranslate();
            }
        });

        this.observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true,
            attributes: true,
            attributeFilter: ['placeholder', 'title', 'value']
        });
    }

    isValidNode(node) {
        if (!node) return false;
        if (node.nodeType === Node.ELEMENT_NODE) {
            if (node.classList.contains('no-translate')) return false;
            const tags = ['SCRIPT', 'STYLE', 'NOSCRIPT'];
            if (tags.includes(node.tagName)) return false;
            return true;
        }
        return false;
    }

    isValidTextNode(node) {
        if (node.nodeType !== Node.TEXT_NODE) return false;
        if (!node.nodeValue.trim()) return false;
        if (node.parentElement && !this.isValidNode(node.parentElement)) return false;
        return true;
    }

    collectTextNodes(root) {
        const textNodes = [];
        if (root.nodeType === Node.TEXT_NODE) {
            if (this.isValidTextNode(root)) textNodes.push(root);
            return textNodes;
        }

        const walker = document.createTreeWalker(
            root,
            NodeFilter.SHOW_TEXT,
            {
                acceptNode: (node) => {
                    return this.isValidTextNode(node) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
                }
            }
        );

        let node;
        while (node = walker.nextNode()) {
            textNodes.push(node);
        }
        return textNodes;
    }

    async setLanguage(lang) {
        if (this.currentLang === lang) return;

        this.currentLang = lang;
        try {
            localStorage.setItem('app_language', lang);
        } catch (e) {
            console.warn('Could not save language to storage:', e);
        }

        if (window.APP_CULTURE) {
            window.APP_CULTURE = lang;
        }

        document.cookie = `.AspNetCore.Culture=c=${lang}|uic=${lang};path=/;max-age=31536000`;

        if (lang === 'en') {
            this.restoreEnglish();
            document.documentElement.lang = 'en';
            return;
        }

        await this.translatePage();
        document.documentElement.lang = lang;
    }

    restoreEnglish() {
        if (this.observer) this.observer.disconnect();

        // Restore Text Nodes
        const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null);
        let node;
        while (node = walker.nextNode()) {
            if (node._originalText) {
                node.nodeValue = node._originalText;
            }
        }

        // Restore Attributes
        const elementsWithAttrs = document.querySelectorAll('[data-org-placeholder], [data-org-title], [data-org-value]');
        elementsWithAttrs.forEach(el => {
            if (el.hasAttribute('data-org-placeholder')) el.setAttribute('placeholder', el.getAttribute('data-org-placeholder'));
            if (el.hasAttribute('data-org-title')) el.setAttribute('title', el.getAttribute('data-org-title'));
            if (el.hasAttribute('data-org-value')) el.value = el.getAttribute('data-org-value');
        });

        if (this.observer) this.setupObserver();
    }

    async processQueue() {
        if (this.mutationQueue.size === 0 && this.attributeQueue.size === 0) return;

        const nodes = Array.from(this.mutationQueue);
        const attrElements = Array.from(this.attributeQueue);

        this.mutationQueue.clear();
        this.attributeQueue.clear();

        // Process in parallel
        await Promise.all([
            this.translateNodes(nodes),
            this.translateAttributes(attrElements)
        ]);
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    async translatePage() {
        // Scan entire body
        const nodes = this.collectTextNodes(document.body);

        const attrSelector = 'input[type="submit"], input[type="button"], input[type="reset"], [placeholder], [title]';
        const attrElements = document.querySelectorAll(attrSelector);

        await Promise.all([
            this.translateNodes(nodes),
            this.translateAttributes(Array.from(attrElements))
        ]);
    }

    async fetchWithRetry(url, options, retries = 3, backoff = 1000) {
        try {
            const response = await fetch(url, options);
            // If success, return response
            if (response.ok) return response;

            // If client error (4xx) that is NOT 429 (Too Many Requests), return response to handle logic/logging
            if (response.status >= 400 && response.status < 500 && response.status !== 429) {
                return response;
            }

            // For 429 or 5xx, throw to trigger retry
            throw new Error(`Retriable error: ${response.status}`);

        } catch (error) {
            if (retries > 0) {
                console.warn(`Translation fetch failed. Retrying in ${backoff}ms... (${retries} retries left)`, error);
                await new Promise(resolve => setTimeout(resolve, backoff));
                return this.fetchWithRetry(url, options, retries - 1, backoff * 2);
            }
            throw error;
        }
    }

    // Unified translation logic
    async translateAttributes(elements) {
        if (this.currentLang === 'en' || !elements || elements.length === 0) return;

        // Collect attributes to translate from provided elements
        const attributeTexts = [];
        const elementsToProcess = [];

        elements.forEach(el => {
            // Filter only valid elements
            if (!this.isValidNode(el)) return;

            elementsToProcess.push(el);
            const tagName = el.tagName;
            const inputTypes = ['submit', 'button', 'reset'];

            // Handle Input Values (Buttons)
            if (tagName === 'INPUT' && inputTypes.includes(el.type)) {
                if (el.value && el.value.trim()) {
                    if (!el.getAttribute('data-org-value')) el.setAttribute('data-org-value', el.value);
                    attributeTexts.push(el.getAttribute('data-org-value'));
                }
            }

            // Handle Standard Attributes
            ['placeholder', 'title'].forEach(attr => {
                if (el.hasAttribute(attr) || el.hasAttribute(`data-org-${attr}`)) {
                    const val = el.getAttribute(`data-org-${attr}`) || el.getAttribute(attr);
                    if (val && val.trim()) {
                        if (!el.getAttribute(`data-org-${attr}`)) el.setAttribute(`data-org-${attr}`, val);
                        attributeTexts.push(val.trim());
                    }
                }
            });
        });

        if (attributeTexts.length === 0) return;

        // Ensure cache
        if (!this.cache.has(this.currentLang)) {
            this.cache.set(this.currentLang, new Map());
        }
        const langCache = this.cache.get(this.currentLang);

        // Identify texts needing API
        const textsToFetch = new Set();
        attributeTexts.forEach(text => {
            if (!langCache.has(text)) textsToFetch.add(text);
        });

        const fetchList = [...textsToFetch];
        if (fetchList.length > 0) this.showLoading(true);

        try {
            // Fetch batch
            if (fetchList.length > 0) {
                const chunkSize = 50;
                for (let i = 0; i < fetchList.length; i += chunkSize) {
                    const chunk = fetchList.slice(i, i + chunkSize);
                    if (chunk.length === 0) continue;

                    // Explicitly send sourceLanguage: 'en'
                    const payload = { texts: chunk, targetLanguage: this.currentLang, sourceLanguage: 'en' };
                    try {
                        const response = await this.fetchWithRetry(this.apiEndpoint, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        });
                        if (response.ok) {
                            const result = await response.json();
                            Object.entries(result).forEach(([k, v]) => langCache.set(k, v));
                        } else {
                            const msg = await response.text();
                            console.error(`Translation server error (${response.status}): ${msg}`, payload);
                        }
                    } catch (err) {
                        console.error("Batch fetch error", err);
                    }
                }
            }

            // Apply translations
            elementsToProcess.forEach(el => {
                // Prevent observer loop
                el._isApplyingTranslation = true;

                // Inputs
                if (el.tagName === 'INPUT' && ['submit', 'button', 'reset'].includes(el.type)) {
                    const original = el.getAttribute('data-org-value');
                    if (original && langCache.has(original)) {
                        const translated = langCache.get(original);
                        if (el.value !== translated) el.value = translated;
                    }
                }

                // Attributes
                ['placeholder', 'title'].forEach(attr => {
                    const original = el.getAttribute(`data-org-${attr}`);
                    if (original && langCache.has(original)) {
                        const translated = langCache.get(original);
                        if (el.getAttribute(attr) !== translated) el.setAttribute(attr, translated);
                    }
                });

                setTimeout(() => el._isApplyingTranslation = false, 0);
            });

        } catch (e) {
            console.error("TranslateAttributes error", e);
        } finally {
            this.showLoading(false);
        }
    }

    // Unified translation logic for TextNodes ONLY
    async translateNodes(nodes) {
        if (this.currentLang === 'en') return;

        // Filter out nodes that don't need translation or invalid
        const validNodes = nodes.filter(n => {
            if (!n._originalText) n._originalText = n.nodeValue;
            return n._originalText.trim().length > 0 && !n.parentElement._isApplyingTranslation;
        });

        if (validNodes.length === 0) return;

        // Ensure cache for lang
        if (!this.cache.has(this.currentLang)) {
            this.cache.set(this.currentLang, new Map());
        }
        const langCache = this.cache.get(this.currentLang);

        // Identify texts needing API
        const textsToFetch = new Set();
        validNodes.forEach(n => {
            const text = n._originalText.trim();
            if (!langCache.has(text)) {
                textsToFetch.add(text);
            }
        });

        const fetchList = [...textsToFetch];

        // Only show loader if we are fetching a significant amount
        if (fetchList.length > 5) this.showLoading(true);

        try {
            // Batch API calls
            if (fetchList.length > 0) {
                const chunkSize = 50;
                for (let i = 0; i < fetchList.length; i += chunkSize) {
                    const chunk = fetchList.slice(i, i + chunkSize).filter(t => t && t.trim().length > 0);
                    if (chunk.length === 0) continue;

                    const payload = { texts: chunk, targetLanguage: this.currentLang, sourceLanguage: 'en' };
                    try {
                        const response = await this.fetchWithRetry(this.apiEndpoint, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        });
                        if (response.ok) {
                            const result = await response.json();
                            Object.entries(result).forEach(([k, v]) => langCache.set(k, v));
                        } else {
                            const msg = await response.text();
                            console.error(`Translation server error (${response.status}): ${msg}`, payload);
                        }
                    } catch (err) {
                        console.error("Batch fetch error", err, payload);
                    }
                }
            }

            // Apply translations to Text Nodes
            validNodes.forEach(node => {
                const source = node._originalText.trim();
                if (langCache.has(source)) {
                    const translated = langCache.get(source);

                    // Mark node as being updated to prevent observer loop
                    // Note: TextNode itself cannot have properties easily observed, but parent can
                    if (node.parentElement) node.parentElement._isApplyingTranslation = true;
                    node._isApplyingTranslation = true;

                    // Apply
                    if (node.nodeValue.includes(source)) {
                        node.nodeValue = node.nodeValue.replace(source, translated);
                    } else {
                        node.nodeValue = translated;
                    }

                    setTimeout(() => {
                        node._isApplyingTranslation = false;
                        if (node.parentElement) node.parentElement._isApplyingTranslation = false;
                    }, 0);
                }
            });

        } catch (e) {
            console.error("TranslateNodes error", e);
        } finally {
            this.showLoading(false);
        }
    }

    async translateElement(element) {
        if (this.currentLang === 'en') return;
        // Handle text nodes
        const nodes = this.collectTextNodes(element);
        await this.translateNodes(nodes);
        // Handle attributes for this element and subtree
        const attrSelector = 'input[type="submit"], input[type="button"], input[type="reset"], [placeholder], [title]';
        const elements = [];
        if (element.matches && element.matches(attrSelector)) elements.push(element);
        element.querySelectorAll(attrSelector).forEach(el => elements.push(el));
        await this.translateAttributes(elements);
    }

    showLoading(show) {
        const loader = document.getElementById('translation-loader');
        if (loader) loader.style.display = show ? 'flex' : 'none';
    }
}

window.TranslationManager = new TranslationManager();
