const sectionLinks = [...document.querySelectorAll('.docs-sidebar a[href^="#"]')];
if (sectionLinks.length && 'IntersectionObserver' in window) {
  const visibility = new Map();
  const observer = new IntersectionObserver(entries => {
    entries.forEach(entry => visibility.set(entry.target.id, entry.isIntersecting ? entry.intersectionRatio : 0));
    const visible = [...visibility].filter(([, ratio]) => ratio > 0).sort((a, b) => b[1] - a[1])[0];
    if (!visible) return;
    sectionLinks.forEach(link => link.classList.toggle('active', link.hash === `#${visible[0]}`));
  }, { rootMargin: '-10% 0px -55% 0px', threshold: [0, 0.25, 0.5] });
  document.querySelectorAll('.docs-content section[id]').forEach(section => observer.observe(section));
}

const tourImage = document.querySelector('[data-tour-image]');
const tourToggle = document.querySelector('[data-tour-toggle]');
if (tourImage && tourToggle) {
  tourToggle.addEventListener('click', () => {
    const playing = tourToggle.getAttribute('aria-pressed') !== 'true';
    tourToggle.setAttribute('aria-pressed', String(playing));
    tourToggle.textContent = playing ? 'Pause tour' : 'Play tour';
    tourImage.src = playing ? tourImage.dataset.animatedSrc : tourImage.dataset.staticSrc;
  });
}
