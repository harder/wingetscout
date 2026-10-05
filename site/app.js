const sectionLinks = [...document.querySelectorAll('.docs-sidebar a[href^="#"]')];
if (sectionLinks.length && 'IntersectionObserver' in window) {
  const observer = new IntersectionObserver(entries => {
    const visible = entries.filter(entry => entry.isIntersecting).sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
    if (!visible) return;
    sectionLinks.forEach(link => link.classList.toggle('active', link.hash === `#${visible.target.id}`));
  }, { rootMargin: '-10% 0px -55% 0px', threshold: [0, 0.25, 0.5] });
  document.querySelectorAll('.docs-content section[id]').forEach(section => observer.observe(section));
}
