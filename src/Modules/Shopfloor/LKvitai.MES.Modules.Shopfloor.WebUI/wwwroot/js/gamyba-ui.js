window.gamybaUi = {
  focusSelect(id) {
    const input = document.getElementById(id);
    if (input) {
      input.focus();
      input.select();
    }
  },
  selectActive() {
    const input = document.activeElement;
    if (input && typeof input.select === "function") input.select();
  },
  print(kind) {
    const name = kind === "roletai" ? "gamyba-print-roletai" : kind === "report" ? "gamyba-print-report" : "gamyba-print-output";
    document.body.classList.add(name);
    let fallback;
    const clean = () => {
      document.body.classList.remove(name);
      window.removeEventListener("afterprint", clean);
      window.removeEventListener("focus", clean);
      clearTimeout(fallback);
    };
    window.addEventListener("afterprint", clean);
    window.addEventListener("focus", clean);
    fallback = setTimeout(clean, 30000);
    window.print();
  }
};
