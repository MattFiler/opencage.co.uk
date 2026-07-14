"use strict";


/* ====== Define JS Constants ====== */
const sidebarToggler = document.getElementById('docs-sidebar-toggler');
const sidebar = document.getElementById('docs-sidebar');
const sidebarLinks = document.querySelectorAll('#docs-sidebar .scrollto');



/* ===== Responsive Sidebar ====== */

function responsiveSidebar() {
	if (!sidebar) return;

	let w = window.innerWidth;
	if (w >= 1200) {
		sidebar.classList.remove('sidebar-hidden');
		sidebar.classList.add('sidebar-visible');
	} else {
		sidebar.classList.remove('sidebar-visible');
		sidebar.classList.add('sidebar-hidden');
	}
}

window.addEventListener('load', responsiveSidebar);
window.addEventListener('resize', responsiveSidebar);

if (sidebarToggler && sidebar) {
	sidebarToggler.addEventListener('click', () => {
		if (sidebar.classList.contains('sidebar-visible')) {
			sidebar.classList.remove('sidebar-visible');
			sidebar.classList.add('sidebar-hidden');
		} else {
			sidebar.classList.remove('sidebar-hidden');
			sidebar.classList.add('sidebar-visible');
		}
	});
}


/* ===== Smooth scrolling ====== */
/*  Note: You need to include smoothscroll.min.js (smooth scroll behavior polyfill) on the page to cover some browsers */
/* Ref: https://github.com/iamdustan/smoothscroll */

sidebarLinks.forEach((sidebarLink) => {
	sidebarLink.addEventListener('click', (e) => {
		e.preventDefault();

		var target = sidebarLink.getAttribute("href").replace('#', '');
		var el = document.getElementById(target);
		if (!el) return;

		el.scrollIntoView({ behavior: 'smooth' });

		// Collapse sidebar after clicking
		if (sidebar && sidebar.classList.contains('sidebar-visible') && window.innerWidth < 1200) {
			sidebar.classList.remove('sidebar-visible');
			sidebar.classList.add('sidebar-hidden');
		}
	});
});


/* ===== Gumshoe ScrollSpy ===== */
/* Ref: https://github.com/cferdinandi/gumshoe  */
if (typeof Gumshoe !== 'undefined' && document.querySelector('#docs-nav a')) {
	var spy = new Gumshoe('#docs-nav a', {
		offset: 69 // sticky header height
	});
}


/* ====== SimpleLightbox Plugin ======= */
/*  Ref: https://github.com/andreknieriem/simplelightbox */
if (typeof SimpleLightbox !== 'undefined' && document.querySelector('.simplelightbox-gallery a')) {
	var lightbox = new SimpleLightbox('.simplelightbox-gallery a', {/* options */});
}
