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

const themePicker = document.querySelector('[data-theme-picker]');
if (themePicker) {
  const image = themePicker.querySelector('[data-theme-image]');
  const title = themePicker.querySelector('[data-theme-title]');
  const caption = themePicker.querySelector('[data-theme-caption]');
  const choices = [...themePicker.querySelectorAll('.theme-choice')];

  choices.forEach(choice => choice.addEventListener('click', () => {
    choices.forEach(button => {
      const selected = button === choice;
      button.setAttribute('aria-pressed', String(selected));
      button.classList.toggle('is-active', selected);
    });
    image.src = choice.dataset.themeSrc;
    image.alt = choice.dataset.themeAlt;
    title.textContent = choice.dataset.themeTitle;
    caption.textContent = choice.dataset.themeCaption;
  }));
}
