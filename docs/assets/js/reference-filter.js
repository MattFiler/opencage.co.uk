"use strict";

/**
 * In-page filter for large reference catalogues
 * (Cathode Entities, Cathode Enums, Behaviour Tree Nodes).
 * Filters the sidebar TOC and matching content sections.
 */
(function () {
	var PLACEHOLDERS = {
		"/docs/behaviour-trees/": "Filter nodes...",
		"/docs/cathode-entities/": "Filter entities...",
		"/docs/cathode-enums/": "Filter enums..."
	};

	function currentPlaceholder() {
		var path = window.location.pathname.replace(/\/index\.html$/i, "/");
		if (!path.endsWith("/")) path += "/";
		if (PLACEHOLDERS[path]) return PLACEHOLDERS[path];

		if (path.indexOf("behaviour-trees") !== -1) return "Filter nodes...";
		if (path.indexOf("cathode-entities") !== -1) return "Filter entities...";
		if (path.indexOf("cathode-enums") !== -1) return "Filter enums...";
		return "Filter entries...";
	}

	function normalize(value) {
		return String(value || "")
			.toLowerCase()
			.replace(/[^a-z0-9]+/g, "");
	}

	function matches(query, label, id) {
		if (!query) return true;
		var q = normalize(query);
		if (!q) return true;
		return normalize(label).indexOf(q) !== -1 || normalize(id).indexOf(q) !== -1;
	}

	function init() {
		var nav = document.getElementById("docs-nav");
		var list = nav && nav.querySelector(".section-items");
		if (!nav || !list) return;

		var items = Array.prototype.slice.call(list.querySelectorAll(":scope > .nav-item"));
		if (!items.length) return;

		var entryItems = items.filter(function (item) {
			return !item.classList.contains("section-title");
		});
		if (!entryItems.length) return;

		var wrap = document.createElement("div");
		wrap.className = "docs-ref-filter";
		wrap.innerHTML =
			'<label class="docs-ref-filter-label" for="docs-ref-filter-input">Filter this page</label>' +
			'<div class="docs-ref-filter-field">' +
				'<i class="fas fa-filter" aria-hidden="true"></i>' +
				'<input id="docs-ref-filter-input" type="search" class="form-control docs-ref-filter-input" placeholder="' + currentPlaceholder() + '" autocomplete="off" spellcheck="false">' +
				'<button type="button" class="docs-ref-filter-clear" aria-label="Clear filter" hidden>&times;</button>' +
			"</div>" +
			'<div class="docs-ref-filter-meta" aria-live="polite"></div>';

		nav.insertBefore(wrap, list);

		var input = wrap.querySelector(".docs-ref-filter-input");
		var clearBtn = wrap.querySelector(".docs-ref-filter-clear");
		var meta = wrap.querySelector(".docs-ref-filter-meta");
		var empty = document.createElement("div");
		empty.className = "docs-ref-filter-empty";
		empty.hidden = true;
		empty.textContent = "No matching entries.";
		list.parentNode.insertBefore(empty, list.nextSibling);

		var sectionMap = {};
		entryItems.forEach(function (item) {
			var link = item.querySelector("a.nav-link");
			if (!link) return;
			var href = link.getAttribute("href") || "";
			var id = href.charAt(0) === "#" ? href.slice(1) : "";
			var label = (link.textContent || "").trim();
			var section = id ? document.getElementById(id) : null;
			sectionMap[id] = {
				item: item,
				link: link,
				id: id,
				label: label,
				section: section && section.classList.contains("docs-section") ? section : null
			};
		});

		function apply(query) {
			var activeQuery = (query || "").trim();
			var shown = 0;

			items.forEach(function (item) {
				if (item.classList.contains("section-title")) {
					item.hidden = false;
					item.classList.remove("is-filtered-out");
					return;
				}

				var link = item.querySelector("a.nav-link");
				var href = link ? link.getAttribute("href") || "" : "";
				var id = href.charAt(0) === "#" ? href.slice(1) : "";
				var entry = sectionMap[id];
				var ok = entry ? matches(activeQuery, entry.label, entry.id) : false;

				item.hidden = !ok;
				item.classList.toggle("is-filtered-out", !ok);
				if (entry && entry.section) {
					entry.section.hidden = !ok;
					entry.section.classList.toggle("is-filtered-out", !ok);
				}
				if (ok) shown += 1;
			});

			// Hide section titles that have no visible children until the next section title
			var currentTitle = null;
			var titleHasVisible = false;
			function flushTitle() {
				if (!currentTitle) return;
				currentTitle.hidden = activeQuery.length > 0 && !titleHasVisible;
				currentTitle.classList.toggle("is-filtered-out", currentTitle.hidden);
			}

			items.forEach(function (item) {
				if (item.classList.contains("section-title")) {
					flushTitle();
					currentTitle = item;
					titleHasVisible = false;
					return;
				}
				if (!item.hidden) titleHasVisible = true;
			});
			flushTitle();

			// Keep group articles visible if any child section is visible
			document.querySelectorAll("article.docs-article").forEach(function (article) {
				var sections = article.querySelectorAll(".docs-section");
				if (!sections.length) return;
				var anyVisible = false;
				sections.forEach(function (section) {
					if (!section.hidden) anyVisible = true;
				});
				// Don't hide the article that only holds breadcrumbs / intro without sections matching pattern
				if (article.querySelector(".docs-section")) {
					article.hidden = activeQuery.length > 0 && !anyVisible;
				}
			});

			clearBtn.hidden = activeQuery.length === 0;
			empty.hidden = !(activeQuery.length > 0 && shown === 0);
			list.hidden = activeQuery.length > 0 && shown === 0;

			if (!activeQuery) {
				meta.textContent = "";
			} else {
				meta.textContent = shown + " of " + entryItems.length + " shown";
			}
		}

		var timer = null;
		input.addEventListener("input", function () {
			window.clearTimeout(timer);
			timer = window.setTimeout(function () {
				apply(input.value);
			}, 60);
		});

		clearBtn.addEventListener("click", function () {
			input.value = "";
			apply("");
			input.focus();
		});

		input.addEventListener("keydown", function (event) {
			if (event.key === "Escape") {
				if (input.value) {
					event.preventDefault();
					input.value = "";
					apply("");
				}
			}
		});

		window.DocsReferenceFilter = {
			clear: function () {
				input.value = "";
				apply("");
			},
			reveal: function (id) {
				// Clear any active filter so the target section is visible in-page
				input.value = "";
				apply("");

				var entry = sectionMap[id];
				if (entry) {
					entry.item.hidden = false;
					entry.item.classList.remove("is-filtered-out");
					if (entry.section) {
						entry.section.hidden = false;
						entry.section.classList.remove("is-filtered-out");
						var article = entry.section.closest("article");
						if (article) article.hidden = false;
					}
				}
			}
		};
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", init);
	} else {
		init();
	}
})();

