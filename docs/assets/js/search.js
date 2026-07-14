"use strict";

(function () {
	var MAX_RESULTS = 12;
	var index = null;
	var indexPromise = null;
	var activeControllers = [];

	function resolveIndexUrl() {
		var scripts = document.getElementsByTagName("script");
		for (var i = 0; i < scripts.length; i++) {
			var src = scripts[i].src || "";
			var match = src.match(/^(.*)\/js\/search\.js(?:\?.*)?$/);
			if (match) return match[1] + "/data/search-index.json";
		}
		return "/docs/assets/data/search-index.json";
	}

	function normalizePath(pathname) {
		return String(pathname || "")
			.replace(/\/index\.html$/i, "/")
			.replace(/\/+$/, "")
			.toLowerCase() || "/";
	}

	function currentPath() {
		return normalizePath(window.location.pathname);
	}

	function loadIndex() {
		if (index) return Promise.resolve(index);
		if (indexPromise) return indexPromise;

		indexPromise = fetch(resolveIndexUrl(), { credentials: "same-origin" })
			.then(function (res) {
				if (!res.ok) throw new Error("Failed to load search index");
				return res.json();
			})
			.then(function (data) {
				index = (data || []).map(normalizeEntry);
				return index;
			})
			.catch(function (err) {
				console.warn("[docs search]", err);
				indexPromise = null;
				return [];
			});

		return indexPromise;
	}

	function normalizeEntry(entry) {
		var keywords = Array.isArray(entry.keywords) ? entry.keywords.join(" ") : (entry.keywords || "");
		var haystack = [entry.title, entry.description, entry.category, keywords]
			.filter(Boolean)
			.join(" ")
			.toLowerCase();
		var compactTitle = String(entry.title || "").toLowerCase().replace(/[^a-z0-9]+/g, "");
		var url = entry.url || "/docs/";
		var hashIndex = url.indexOf("#");
		var path = hashIndex === -1 ? url : url.slice(0, hashIndex);
		var hash = hashIndex === -1 ? "" : url.slice(hashIndex);

		return {
			title: entry.title || "Untitled",
			description: entry.description || "",
			url: url,
			category: entry.category || "Docs",
			type: entry.type || (hash ? "symbol" : "page"),
			haystack: haystack,
			compactTitle: compactTitle,
			path: normalizePath(path),
			hash: hash
		};
	}

	function tokenize(query) {
		return query
			.toLowerCase()
			.trim()
			.split(/[^a-z0-9_/.-]+/i)
			.filter(function (token) { return token.length > 0; });
	}

	function scoreEntry(entry, tokens, rawQuery) {
		if (!tokens.length) return 0;

		var title = entry.title.toLowerCase();
		var desc = entry.description.toLowerCase();
		var category = entry.category.toLowerCase();
		var compactQuery = rawQuery.replace(/[^a-z0-9]+/g, "");
		var score = 0;
		var matched = 0;

		tokens.forEach(function (token) {
			var compactToken = token.replace(/[^a-z0-9]+/g, "");
			var tokenScore = 0;

			if (title === token || entry.compactTitle === compactToken) tokenScore = 140;
			else if (title.indexOf(token) === 0 || entry.compactTitle.indexOf(compactToken) === 0) tokenScore = 100;
			else if (title.indexOf(token) !== -1 || entry.compactTitle.indexOf(compactToken) !== -1) tokenScore = 80;
			else if (category.indexOf(token) !== -1) tokenScore = 40;
			else if (desc.indexOf(token) !== -1) tokenScore = 30;
			else if (entry.haystack.indexOf(token) !== -1) tokenScore = 20;

			if (tokenScore) {
				matched += 1;
				score += tokenScore;
			}
		});

		if (matched !== tokens.length) return 0;

		if (rawQuery && title.indexOf(rawQuery) !== -1) score += 25;
		if (compactQuery && entry.compactTitle === compactQuery) score += 40;
		if (entry.type === "symbol") score += 8;
		if (entry.path === currentPath()) score += 35;

		return score;
	}

	function search(query) {
		var raw = (query || "").toLowerCase().trim();
		var tokens = tokenize(raw);
		if (!tokens.length || !index) return [];

		return index
			.map(function (entry) {
				return { entry: entry, score: scoreEntry(entry, tokens, raw) };
			})
			.filter(function (item) { return item.score > 0; })
			.sort(function (a, b) {
				if (b.score !== a.score) return b.score - a.score;
				if (a.entry.type !== b.entry.type) return a.entry.type === "symbol" ? -1 : 1;
				return a.entry.title.localeCompare(b.entry.title);
			})
			.slice(0, MAX_RESULTS)
			.map(function (item) { return item.entry; });
	}

	function escapeHtml(value) {
		return String(value)
			.replace(/&/g, "&amp;")
			.replace(/</g, "&lt;")
			.replace(/>/g, "&gt;")
			.replace(/"/g, "&quot;")
			.replace(/'/g, "&#39;");
	}

	function highlight(text, tokens) {
		var safe = escapeHtml(text);
		if (!tokens.length) return safe;

		tokens
			.slice()
			.sort(function (a, b) { return b.length - a.length; })
			.forEach(function (token) {
				var pattern = new RegExp("(" + token.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + ")", "ig");
				safe = safe.replace(pattern, '<mark class="docs-search-highlight">$1</mark>');
			});

		return safe;
	}

	function flashTarget(el) {
		if (!el) return;
		el.classList.remove("docs-search-flash");
		// Force restart if the class was already present
		void el.offsetWidth;
		el.classList.add("docs-search-flash");
		window.setTimeout(function () {
			el.classList.remove("docs-search-flash");
		}, 1600);
	}

	function jumpToHash(hash) {
		var id = decodeURIComponent(String(hash || "").replace(/^#/, ""));
		if (!id) return false;

		if (window.DocsReferenceFilter && typeof window.DocsReferenceFilter.reveal === "function") {
			window.DocsReferenceFilter.reveal(id);
		}

		var el = document.getElementById(id);
		if (!el) return false;

		if (history.pushState) {
			history.pushState(null, "", "#" + id);
		} else {
			window.location.hash = id;
		}

		window.requestAnimationFrame(function () {
			el.scrollIntoView({ behavior: "smooth", block: "start" });
			flashTarget(el);
		});

		return true;
	}

	function navigateTo(url) {
		var target;
		try {
			target = new URL(url, window.location.href);
		} catch (e) {
			window.location.href = url;
			return;
		}

		var samePage = normalizePath(target.pathname) === currentPath();
		if (samePage && target.hash) {
			if (jumpToHash(target.hash)) return;
		}

		window.location.href = target.href;
	}

	function renderResults(container, results, query) {
		var tokens = tokenize(query);

		if (!results.length) {
			container.innerHTML = '<div class="docs-search-empty">No matching pages or entries.</div>';
			container.classList.add("is-open");
			return;
		}

		container.innerHTML = results.map(function (entry, idx) {
			var typeBadge = entry.type === "symbol" ? ' <span class="docs-search-item-type">entry</span>' : "";
			return (
				'<a class="docs-search-item' + (idx === 0 ? " is-active" : "") + '" href="' + escapeHtml(entry.url) + '" data-index="' + idx + '">' +
					'<span class="docs-search-item-category">' + escapeHtml(entry.category) + typeBadge + "</span>" +
					'<span class="docs-search-item-title">' + highlight(entry.title, tokens) + "</span>" +
					'<span class="docs-search-item-desc">' + highlight(entry.description, tokens) + "</span>" +
				"</a>"
			);
		}).join("");

		container.classList.add("is-open");
	}

	function closeResults(container) {
		container.classList.remove("is-open");
		container.innerHTML = "";
	}

	function getItems(container) {
		return Array.prototype.slice.call(container.querySelectorAll(".docs-search-item"));
	}

	function setActive(container, index) {
		var items = getItems(container);
		if (!items.length) return;

		var next = (index + items.length) % items.length;
		items.forEach(function (item, i) {
			item.classList.toggle("is-active", i === next);
		});
	}

	function getActive(container) {
		return container.querySelector(".docs-search-item.is-active") || getItems(container)[0] || null;
	}

	function bindSearchRoot(root) {
		var form = root.querySelector(".search-form");
		var input = root.querySelector(".search-input");
		if (!form || !input) return;

		var results = document.createElement("div");
		results.className = "docs-search-results";
		results.setAttribute("role", "listbox");
		results.setAttribute("aria-label", "Search results");
		root.appendChild(results);

		var state = { query: "", open: false };
		activeControllers.push({
			root: root,
			input: input,
			results: results,
			close: function () {
				closeResults(results);
				state.open = false;
			}
		});

		function update() {
			var query = input.value || "";
			state.query = query;

			if (!query.trim()) {
				closeResults(results);
				state.open = false;
				return;
			}

			loadIndex().then(function () {
				if (input.value !== query) return;
				var matches = search(query);
				renderResults(results, matches, query);
				state.open = true;
			});
		}

		function go(url) {
			closeResults(results);
			state.open = false;
			navigateTo(url);
		}

		var debounceTimer = null;
		input.setAttribute("autocomplete", "off");
		input.setAttribute("spellcheck", "false");
		input.setAttribute("role", "combobox");
		input.setAttribute("aria-autocomplete", "list");
		input.setAttribute("aria-expanded", "false");

		input.addEventListener("input", function () {
			window.clearTimeout(debounceTimer);
			debounceTimer = window.setTimeout(update, 80);
		});

		input.addEventListener("focus", function () {
			loadIndex().then(function () {
				if ((input.value || "").trim()) update();
			});
		});

		form.addEventListener("submit", function (event) {
			event.preventDefault();
			var active = getActive(results);
			if (active) {
				go(active.getAttribute("href"));
				return;
			}

			loadIndex().then(function () {
				var matches = search(input.value || "");
				if (matches[0]) go(matches[0].url);
			});
		});

		input.addEventListener("keydown", function (event) {
			if (!results.classList.contains("is-open")) {
				if (event.key === "ArrowDown" && (input.value || "").trim()) {
					event.preventDefault();
					update();
				}
				return;
			}

			var items = getItems(results);
			var current = items.findIndex(function (item) { return item.classList.contains("is-active"); });

			if (event.key === "ArrowDown") {
				event.preventDefault();
				setActive(results, current + 1);
			} else if (event.key === "ArrowUp") {
				event.preventDefault();
				setActive(results, current - 1);
			} else if (event.key === "Escape") {
				event.preventDefault();
				closeResults(results);
				input.blur();
			} else if (event.key === "Enter") {
				var active = getActive(results);
				if (active) {
					event.preventDefault();
					go(active.getAttribute("href"));
				}
			}
		});

		results.addEventListener("mousedown", function (event) {
			event.preventDefault();
		});

		results.addEventListener("click", function (event) {
			var item = event.target.closest(".docs-search-item");
			if (!item) return;
			event.preventDefault();
			go(item.getAttribute("href"));
		});
	}

	function init() {
		var roots = document.querySelectorAll(".top-search-box, .main-search-box");
		if (!roots.length) return;

		roots.forEach(bindSearchRoot);
		loadIndex();

		document.addEventListener("click", function (event) {
			activeControllers.forEach(function (controller) {
				if (!controller.root.contains(event.target)) controller.close();
			});
		});
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", init);
	} else {
		init();
	}
})();
