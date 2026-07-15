"use strict";

(function () {
	var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
	var supportsViewTransitions = "startViewTransition" in document;
	var docsRoot = detectDocsRoot();

	document.documentElement.classList.add("docs-motion");

	function detectDocsRoot() {
		var scripts = document.getElementsByTagName("script");
		for (var i = 0; i < scripts.length; i++) {
			var src = scripts[i].src || "";
			var match = src.match(/^(.*\/docs\/)assets\/js\/motion\.js/);
			if (match) return match[1];
		}
		// Fallback from common path depths
		var path = window.location.pathname;
		var idx = path.indexOf("/docs/");
		if (idx !== -1) return path.slice(0, idx + 6);
		return "/docs/";
	}

	function isInternalDocsLink(anchor) {
		if (!anchor || !anchor.href) return false;
		if (anchor.target && anchor.target !== "_self") return false;
		if (anchor.hasAttribute("download")) return false;
		if (anchor.getAttribute("href") && anchor.getAttribute("href").charAt(0) === "#") return false;

		try {
			var url = new URL(anchor.href, window.location.href);
			if (url.origin !== window.location.origin) return false;
			if (url.pathname === window.location.pathname && url.hash) return false;
			return url.pathname.indexOf("/docs") !== -1 || url.pathname.indexOf(docsRoot) !== -1;
		} catch (e) {
			return false;
		}
	}

	window.addEventListener("pageshow", function (event) {
		if (event.persisted) {
			document.body.classList.remove("docs-leaving");
		}
	});

	/* Page leave fade (fallback when View Transitions aren't used) */
	if (!reduceMotion) {
		document.addEventListener("click", function (event) {
			var anchor = event.target.closest("a");
			if (!isInternalDocsLink(anchor)) return;
			if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
			if (event.button !== 0) return;

			// Native View Transitions handle this in supporting browsers
			if (supportsViewTransitions) return;

			event.preventDefault();
			document.body.classList.add("docs-leaving");
			window.setTimeout(function () {
				window.location.href = anchor.href;
			}, 180);
		});
	}

	/* Staggered reveals */
	function initReveals() {
		var items = document.querySelectorAll(".docs-reveal");
		if (!items.length) return;

		if (reduceMotion || !("IntersectionObserver" in window)) {
			items.forEach(function (el) { el.classList.add("is-visible"); });
			return;
		}

		var observer = new IntersectionObserver(function (entries) {
			entries.forEach(function (entry) {
				if (!entry.isIntersecting) return;
				var el = entry.target;
				var delay = Number(el.getAttribute("data-reveal-delay") || 0);
				window.setTimeout(function () {
					el.classList.add("is-visible");
				}, delay);
				observer.unobserve(el);
			});
		}, { threshold: 0.12, rootMargin: "0px 0px -8% 0px" });

		items.forEach(function (el) { observer.observe(el); });
	}

	/* Expand / collapse panels */
	function setPanelOpen(panel, open, animate) {
		var body = panel.querySelector(".docs-panel-body");
		var toggle = panel.querySelector(".docs-panel-toggle");
		if (!body || !toggle) return;

		panel.classList.toggle("is-open", open);
		toggle.setAttribute("aria-expanded", open ? "true" : "false");

		if (reduceMotion || !animate) {
			body.style.height = open ? "auto" : "0px";
			return;
		}

		if (open) {
			body.style.height = body.scrollHeight + "px";
			var onEnd = function (e) {
				if (e.propertyName !== "height") return;
				body.style.height = "auto";
				body.removeEventListener("transitionend", onEnd);
			};
			body.addEventListener("transitionend", onEnd);
		} else {
			body.style.height = body.scrollHeight + "px";
			// Force reflow so the browser registers the start height
			body.offsetHeight; // eslint-disable-line no-unused-expressions
			body.style.height = "0px";
		}
	}

	function initPanels() {
		var panels = document.querySelectorAll(".docs-panel");
		panels.forEach(function (panel) {
			var body = panel.querySelector(".docs-panel-body");
			var toggle = panel.querySelector(".docs-panel-toggle");
			if (!body || !toggle) return;

			var startOpen = panel.classList.contains("is-open") || panel.getAttribute("data-open") === "true";
			setPanelOpen(panel, startOpen, false);

			toggle.addEventListener("click", function () {
				var willOpen = !panel.classList.contains("is-open");
				setPanelOpen(panel, willOpen, true);
			});
		});
	}

	/* Soft enter for docs sections as you scroll */
	function initSections() {
		var sections = document.querySelectorAll(".docs-section");
		if (!sections.length) return;

		sections.forEach(function (section) {
			section.classList.add("docs-section-enter");
		});

		if (reduceMotion || !("IntersectionObserver" in window)) {
			sections.forEach(function (section) { section.classList.add("is-visible"); });
			return;
		}

		var observer = new IntersectionObserver(function (entries) {
			entries.forEach(function (entry) {
				if (!entry.isIntersecting) return;
				entry.target.classList.add("is-visible");
				observer.unobserve(entry.target);
			});
		}, { threshold: 0.08, rootMargin: "0px 0px -5% 0px" });

		sections.forEach(function (section) { observer.observe(section); });
	}

	function syncNavbarLogoContrast() {
		var header = document.querySelector(".header");
		var logo = document.querySelector(".site-logo .logo-icon");
		if (!header || !logo) return;

		function parseRgb(color) {
			if (!color || color === "transparent") return null;
			var match = color.match(/rgba?\(\s*([0-9.]+)\s*,\s*([0-9.]+)\s*,\s*([0-9.]+)(?:\s*,\s*([0-9.]+))?/);
			if (!match) return null;
			var alpha = match[4] === undefined ? 1 : parseFloat(match[4]);
			if (alpha === 0) return null;
			return {
				r: parseFloat(match[1]),
				g: parseFloat(match[2]),
				b: parseFloat(match[3])
			};
		}

		function luminance(rgb) {
			return (0.2126 * rgb.r + 0.7152 * rgb.g + 0.0722 * rgb.b) / 255;
		}

		function update() {
			if (document.documentElement.getAttribute("data-theme") === "dark") {
				logo.classList.add("logo-on-dark");
				return;
			}
			var bg = parseRgb(window.getComputedStyle(header).backgroundColor);
			var onDark = bg ? luminance(bg) < 0.45 : false;
			logo.classList.toggle("logo-on-dark", onDark);
		}

		update();
		window.setTimeout(update, 100);
		window.setTimeout(update, 500);
		window.setTimeout(update, 1500);

		if ("MutationObserver" in window) {
			var observer = new MutationObserver(function () { update(); });
			observer.observe(document.documentElement, {
				attributes: true,
				attributeFilter: ["data-darkreader-scheme", "data-darkreader-mode", "data-theme", "class", "style"]
			});
			observer.observe(header, {
				attributes: true,
				attributeFilter: ["style", "class"]
			});
		}
	}

	function getPreferredTheme() {
		try {
			var stored = window.localStorage.getItem("docs-theme");
			if (stored === "light" || stored === "dark") return stored;
		} catch (e) { /* ignore */ }
		if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
			return "dark";
		}
		return "light";
	}

	function applyTheme(theme) {
		var next = theme === "dark" ? "dark" : "light";
		document.documentElement.setAttribute("data-theme", next);
		try {
			window.localStorage.setItem("docs-theme", next);
		} catch (e) { /* ignore */ }
		var toggle = document.querySelector(".docs-theme-toggle");
		if (toggle) {
			toggle.setAttribute("aria-label", next === "dark" ? "Switch to light mode" : "Switch to dark mode");
			toggle.setAttribute("title", next === "dark" ? "Switch to light mode" : "Switch to dark mode");
		}
	}

	function initThemeToggle() {
		var utilities = document.querySelector(".docs-top-utilities");
		if (!utilities || utilities.querySelector(".docs-theme-toggle")) return;

		var button = document.createElement("button");
		button.type = "button";
		button.className = "docs-theme-toggle";
		button.innerHTML =
			'<i class="fa-solid fa-moon docs-theme-icon-moon" aria-hidden="true"></i>' +
			'<i class="fa-solid fa-sun docs-theme-icon-sun" aria-hidden="true"></i>';

		var searchBox = utilities.querySelector(".top-search-box");
		if (searchBox && searchBox.nextSibling) {
			utilities.insertBefore(button, searchBox.nextSibling);
		} else if (searchBox) {
			utilities.appendChild(button);
		} else {
			utilities.insertBefore(button, utilities.firstChild);
		}

		applyTheme(document.documentElement.getAttribute("data-theme") || getPreferredTheme());

		button.addEventListener("click", function () {
			var current = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
			applyTheme(current === "dark" ? "light" : "dark");
		});

		if (window.matchMedia) {
			var media = window.matchMedia("(prefers-color-scheme: dark)");
			var onChange = function (event) {
				try {
					if (window.localStorage.getItem("docs-theme")) return;
				} catch (e) { return; }
				applyTheme(event.matches ? "dark" : "light");
			};
			if (media.addEventListener) media.addEventListener("change", onChange);
			else if (media.addListener) media.addListener(onChange);
		}
	}

	function boot() {
		initPanels();
		initReveals();
		initSections();
		initThemeToggle();
		syncNavbarLogoContrast();
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", boot);
	} else {
		boot();
	}
})();
