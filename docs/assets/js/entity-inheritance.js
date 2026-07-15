"use strict";

/**
 * Cathode Entities: expand each entity section to show members
 * inherited through the full parent chain, tagged with source links.
 */
(function () {
	var MEMBER_LABELS = [
		"Parameters:",
		"Inputs:",
		"Outputs:",
		"Targets:",
		"States:",
		"Internals:",
		"Methods:"
	];

	function normalizeName(value) {
		return String(value || "")
			.toLowerCase()
			.replace(/[^a-z0-9_]+/g, "");
	}

	function itemName(li) {
		var code = li.querySelector('code[title="Name"]');
		return code ? normalizeName(code.textContent) : normalizeName(li.textContent);
	}

	function parseParent(section) {
		var link = section.querySelector(':scope > p > a.scrollto[href^="#"]');
		if (!link) return null;
		var p = link.parentElement;
		if (!p || (p.textContent || "").indexOf("Inherits from") === -1) return null;
		return decodeURIComponent((link.getAttribute("href") || "").replace(/^#/, ""));
	}

	function parseGroups(section) {
		var groups = {};
		var kids = Array.prototype.slice.call(section.children);
		for (var i = 0; i < kids.length; i++) {
			var el = kids[i];
			if (el.tagName !== "H5") continue;
			var label = (el.textContent || "").trim();
			if (MEMBER_LABELS.indexOf(label) === -1) continue;
			var ul = kids[i + 1];
			if (!ul || ul.tagName !== "UL") continue;
			groups[label] = {
				heading: el,
				list: ul,
				items: Array.prototype.slice.call(ul.children).filter(function (n) {
					return n.tagName === "LI";
				})
			};
		}
		return groups;
	}

	function collectOwnNames(groups) {
		var names = {};
		MEMBER_LABELS.forEach(function (label) {
			var g = groups[label];
			if (!g) return;
			g.items.forEach(function (li) {
				names[label + "::" + itemName(li)] = true;
			});
		});
		return names;
	}

	function ancestorChain(id, byId) {
		var chain = [];
		var seen = {};
		var current = byId[id];
		var guard = 0;
		while (current && current.parent && guard++ < 32) {
			if (seen[current.parent]) break;
			seen[current.parent] = true;
			var parent = byId[current.parent];
			if (!parent) break;
			chain.push(parent);
			current = parent;
		}
		return chain;
	}

	function inheritedEntries(sectionId, byId) {
		var self = byId[sectionId];
		if (!self) return { entries: [], count: 0 };

		var blocked = collectOwnNames(self.groups);
		var entries = [];
		var count = 0;

		ancestorChain(sectionId, byId).forEach(function (ancestor) {
			MEMBER_LABELS.forEach(function (label) {
				var g = ancestor.groups[label];
				if (!g) return;
				g.items.forEach(function (li) {
					var key = label + "::" + itemName(li);
					if (blocked[key]) return;
					blocked[key] = true;
					entries.push({
						label: label,
						sourceId: ancestor.id,
						sourceTitle: ancestor.title,
						li: li
					});
					count += 1;
				});
			});
		});

		return { entries: entries, count: count };
	}

	function makeTag(sourceId, sourceTitle) {
		var tag = document.createElement("a");
		tag.className = "entity-inherit-tag scrollto";
		tag.href = "#" + sourceId;
		tag.textContent = sourceTitle;
		tag.title = "Inherited from " + sourceTitle;
		return tag;
	}

	function ensureGroup(section, label) {
		var existing = null;
		var kids = Array.prototype.slice.call(section.children);
		for (var i = 0; i < kids.length; i++) {
			if (kids[i].tagName === "H5" && (kids[i].textContent || "").trim() === label) {
				var ul = kids[i + 1];
				if (ul && ul.tagName === "UL") {
					existing = { heading: kids[i], list: ul };
					break;
				}
			}
		}
		if (existing) return existing;

		var wrap = document.createElement("div");
		wrap.className = "entity-inherited-group";
		wrap.setAttribute("data-entity-inherited", "1");

		var h5 = document.createElement("h5");
		h5.textContent = label;
		var list = document.createElement("ul");
		wrap.appendChild(h5);
		wrap.appendChild(list);
		section.appendChild(wrap);
		return { heading: h5, list: list, createdWrap: wrap };
	}

	function collapse(section) {
		section.querySelectorAll("[data-entity-inherited='1']").forEach(function (el) {
			el.parentNode.removeChild(el);
		});
		section.classList.remove("is-showing-inherited");
		var btn = section.querySelector(".entity-inherit-toggle");
		if (btn) {
			btn.classList.remove("is-open");
			btn.setAttribute("aria-expanded", "false");
			var count = btn.getAttribute("data-count") || "0";
			btn.innerHTML = 'Show inherited <span class="entity-inherit-toggle-count">(' + count + ")</span>";
		}
	}

	function expand(section, byId) {
		collapse(section);
		var result = inheritedEntries(section.id, byId);
		if (!result.count) return;

		var listsByLabel = {};
		result.entries.forEach(function (entry) {
			if (!listsByLabel[entry.label]) {
				listsByLabel[entry.label] = ensureGroup(section, entry.label);
			}
			var clone = entry.li.cloneNode(true);
			clone.classList.add("entity-inherited");
			clone.setAttribute("data-entity-inherited", "1");
			clone.appendChild(makeTag(entry.sourceId, entry.sourceTitle));
			listsByLabel[entry.label].list.appendChild(clone);
		});

		section.classList.add("is-showing-inherited");
		var btn = section.querySelector(".entity-inherit-toggle");
		if (btn) {
			btn.classList.add("is-open");
			btn.setAttribute("aria-expanded", "true");
			var count = btn.getAttribute("data-count") || String(result.count);
			btn.innerHTML = 'Hide inherited <span class="entity-inherit-toggle-count">(' + count + ")</span>";
		}
	}

	function isInheritsLine(el) {
		if (!el || el.tagName !== "P") return false;
		return (el.textContent || "").indexOf("Inherits from") !== -1;
	}

	function init() {
		var sections = Array.prototype.slice.call(document.querySelectorAll("section.docs-section[id]"));
		if (!sections.length) return;

		var byId = {};
		sections.forEach(function (section) {
			var heading = section.querySelector(".section-heading");
			byId[section.id] = {
				id: section.id,
				title: heading ? heading.textContent.trim() : section.id,
				section: section,
				parent: parseParent(section),
				groups: parseGroups(section)
			};
		});

		sections.forEach(function (section) {
			var info = byId[section.id];
			if (!info || !info.parent) return;

			var result = inheritedEntries(section.id, byId);
			if (!result.count) return;

			var inheritsLine = null;
			Array.prototype.some.call(section.children, function (child) {
				if (isInheritsLine(child)) {
					inheritsLine = child;
					return true;
				}
				return false;
			});

			if (inheritsLine) {
				inheritsLine.hidden = true;
				inheritsLine.setAttribute("aria-hidden", "true");
			}

			var btn = document.createElement("button");
			btn.type = "button";
			btn.className = "entity-inherit-toggle";
			btn.setAttribute("aria-expanded", "false");
			btn.setAttribute("data-count", String(result.count));
			btn.innerHTML = 'Show inherited <span class="entity-inherit-toggle-count">(' + result.count + ")</span>";

			btn.addEventListener("click", function () {
				if (section.classList.contains("is-showing-inherited")) {
					collapse(section);
				} else {
					expand(section, byId);
				}
			});

			if (inheritsLine && inheritsLine.parentNode === section) {
				inheritsLine.insertAdjacentElement("afterend", btn);
			} else {
				var h2 = section.querySelector(".section-heading");
				if (h2) h2.insertAdjacentElement("afterend", btn);
				else section.insertBefore(btn, section.firstChild);
			}
		});
	}

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", init);
	} else {
		init();
	}
})();
