(() => {
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  const autoRevealSelectors = [
    ".proof-item",
    ".feature-card",
    ".timeline-step",
    ".screen-card",
    ".professional-grid > div",
    ".docs-grid > a",
    ".faq-list details",
    ".download > *",
    ".site-footer > *"
  ];

  document.querySelectorAll(autoRevealSelectors.join(",")).forEach((item) => {
    item.classList.add("reveal");
  });

  const revealItems = Array.from(document.querySelectorAll(".reveal"));
  const grouped = new Map();
  revealItems.forEach((item) => {
    const group = item.closest("section, .proof-band, .download, .site-footer, .hero") || item.parentElement || document.body;
    if (!grouped.has(group)) grouped.set(group, []);
    grouped.get(group).push(item);
  });

  grouped.forEach((items) => {
    items.forEach((item, index) => {
      if (!item.style.getPropertyValue("--reveal-delay") && !item.classList.contains("delay-1") && !item.classList.contains("delay-2") && !item.classList.contains("delay-3")) {
        item.style.setProperty("--reveal-delay", `${Math.min(index * 92, 520)}ms`);
      }
    });
  });

  if (reduceMotion || !("IntersectionObserver" in window)) {
    revealItems.forEach((item) => item.classList.add("is-visible"));
  } else {
    const observer = new IntersectionObserver(
      (entries) => {
        entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
          .forEach((entry) => {
            entry.target.classList.add("is-visible");
            observer.unobserve(entry.target);
          });
      },
      { threshold: 0.14, rootMargin: "0px 0px -6% 0px" }
    );

    revealItems.forEach((item) => observer.observe(item));
  }

  const header = document.querySelector(".site-header");
  const setHeaderState = () => {
    if (!header) return;
    header.classList.toggle("is-scrolled", window.scrollY > 16);
  };

  setHeaderState();
  window.addEventListener("scroll", setHeaderState, { passive: true });

  const waveCanvas = document.querySelector("#liquidWaveCanvas");
  const liquidState = {
    ctx: null,
    dpr: 1,
    width: 0,
    height: 0,
    lastScrollY: window.scrollY,
    scrollEnergy: 0,
    clickEnergy: 0,
    impulses: [],
    running: false
  };

  const resizeLiquidCanvas = () => {
    if (!waveCanvas) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = waveCanvas.getBoundingClientRect();
    liquidState.dpr = dpr;
    liquidState.width = Math.max(1, Math.floor(rect.width * dpr));
    liquidState.height = Math.max(1, Math.floor(rect.height * dpr));
    waveCanvas.width = liquidState.width;
    waveCanvas.height = liquidState.height;
  };

  const addLiquidImpulse = (clientX, clientY, strength = 1) => {
    if (!waveCanvas || reduceMotion) return;
    const rect = waveCanvas.getBoundingClientRect();
    liquidState.impulses.push({
      x: (clientX - rect.left) * liquidState.dpr,
      y: (clientY - rect.top) * liquidState.dpr,
      age: 0,
      strength: clamp(strength, .25, 2.2)
    });
    liquidState.clickEnergy = Math.min(2.8, liquidState.clickEnergy + strength * .42);
  };

  const drawLiquidWave = (ctx, time, layer) => {
    const w = liquidState.width;
    const h = liquidState.height;
    const base = h * layer.base;
    const amp = h * (layer.amp + liquidState.scrollEnergy * layer.scrollAmp + liquidState.clickEnergy * layer.clickAmp);
    const step = Math.max(14, w / 126);
    const points = [];

    for (let x = -step; x <= w + step; x += step) {
      const n = x / w;
      let y = base;
      y += Math.sin(n * layer.freqA + time * layer.speedA + layer.phase) * amp;
      y += Math.sin(n * layer.freqB - time * layer.speedB + layer.phase * .7) * amp * .48;
      y += Math.sin(n * 25 + time * .00082 + liquidState.scrollEnergy * 3.8) * amp * .18;

      for (const impulse of liquidState.impulses) {
        const dx = x - impulse.x;
        const dy = base - impulse.y;
        const distance = Math.hypot(dx, dy);
        const radius = 80 * liquidState.dpr + impulse.age * 520 * liquidState.dpr;
        const envelope = Math.exp(-Math.pow((distance - radius) / (135 * liquidState.dpr), 2));
        const ripple = Math.sin(distance * .021 - impulse.age * 12.5 + layer.phase);
        y += ripple * envelope * impulse.strength * h * layer.impulseAmp * (1 - Math.min(1, impulse.age / 1.15));
      }

      points.push([x, y]);
    }

    const strokePath = (selectedPoints) => {
      ctx.beginPath();
      selectedPoints.forEach(([x, y], index) => {
        if (index === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });
      ctx.stroke();
    };

    ctx.strokeStyle = layer.stroke;
    ctx.lineWidth = layer.width * liquidState.dpr;
    ctx.globalAlpha = layer.alpha;
    strokePath(points);

    if (layer.shineAlpha) {
      const shineWidth = (layer.shineWidth || 220) * liquidState.dpr;
      const travel = w + shineWidth * 2;
      const center = ((time * (layer.shineSpeed || .11) + layer.phase * 960) % travel) - shineWidth;
      const shinePoints = points.filter(([x]) => Math.abs(x - center) <= shineWidth);
      if (shinePoints.length > 1) {
        const gradient = ctx.createLinearGradient(center - shineWidth, 0, center + shineWidth, 0);
        gradient.addColorStop(0, "rgba(255,255,255,0)");
        gradient.addColorStop(.36, layer.shineColorSoft || "rgba(104,234,216,.10)");
        gradient.addColorStop(.50, layer.shineColor || "rgba(240,255,252,.70)");
        gradient.addColorStop(.64, layer.shineColorSoft || "rgba(104,234,216,.10)");
        gradient.addColorStop(1, "rgba(255,255,255,0)");

        ctx.save();
        ctx.strokeStyle = gradient;
        ctx.lineWidth = (layer.width + .55) * liquidState.dpr;
        ctx.globalAlpha = layer.shineAlpha;
        ctx.shadowBlur = 11 * liquidState.dpr;
        ctx.shadowColor = layer.shineColor || "rgba(104,234,216,.55)";
        strokePath(shinePoints);
        ctx.restore();
      }
    }
  };

  const animateLiquid = (time) => {
    const ctx = liquidState.ctx;
    if (!ctx) return;
    ctx.clearRect(0, 0, liquidState.width, liquidState.height);
    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    drawLiquidWave(ctx, time, { base: .18, amp: .018, scrollAmp: .018, clickAmp: .010, impulseAmp: .046, freqA: 7.0, freqB: 13.5, speedA: .00052, speedB: .00037, phase: .4, width: 1.12, alpha: .58, stroke: "rgba(104,234,216,.34)", shineAlpha: .62, shineSpeed: .12, shineWidth: 260, shineColor: "rgba(238,255,252,.74)", shineColorSoft: "rgba(104,234,216,.18)" });
    drawLiquidWave(ctx, time, { base: .48, amp: .023, scrollAmp: .024, clickAmp: .014, impulseAmp: .056, freqA: 8.6, freqB: 16.2, speedA: .00042, speedB: .00031, phase: 2.1, width: 1.02, alpha: .45, stroke: "rgba(114,167,255,.26)", shineAlpha: .48, shineSpeed: .10, shineWidth: 310, shineColor: "rgba(228,241,255,.55)", shineColorSoft: "rgba(114,167,255,.13)" });
    drawLiquidWave(ctx, time, { base: .77, amp: .019, scrollAmp: .019, clickAmp: .010, impulseAmp: .042, freqA: 6.7, freqB: 15.4, speedA: .00038, speedB: .00029, phase: 4.4, width: .92, alpha: .36, stroke: "rgba(245,177,79,.20)", shineAlpha: .34, shineSpeed: .085, shineWidth: 290, shineColor: "rgba(255,236,191,.50)", shineColorSoft: "rgba(245,177,79,.12)" });

    liquidState.scrollEnergy *= .94;
    liquidState.clickEnergy *= .92;
    liquidState.impulses.forEach((impulse) => { impulse.age += 1 / 60; });
    liquidState.impulses = liquidState.impulses.filter((impulse) => impulse.age < 1.35);
    window.requestAnimationFrame(animateLiquid);
  };

  if (waveCanvas && !reduceMotion) {
    liquidState.ctx = waveCanvas.getContext("2d", { alpha: true });
    resizeLiquidCanvas();
    window.addEventListener("resize", resizeLiquidCanvas, { passive: true });
    window.addEventListener("scroll", () => {
      const delta = window.scrollY - liquidState.lastScrollY;
      liquidState.lastScrollY = window.scrollY;
      liquidState.scrollEnergy = Math.min(4.4, liquidState.scrollEnergy + Math.min(140, Math.abs(delta)) / 34);
    }, { passive: true });
    window.requestAnimationFrame(animateLiquid);
  }

  const lightbox = document.querySelector("#screenshotLightbox");
  const lightboxImage = document.querySelector("#lightboxImage");
  const lightboxTitle = document.querySelector("#lightboxTitle");
  const stage = lightbox?.querySelector(".lightbox-stage");
  const zoomReadout = lightbox?.querySelector(".zoom-readout");
  const triggers = Array.from(document.querySelectorAll(".screenshot-trigger"));

  if (!lightbox || !lightboxImage || !stage || !zoomReadout) return;

  const MIN_SCALE = 0.72;
  const MAX_SCALE = 4.5;
  let scale = 1;
  let translateX = 0;
  let translateY = 0;
  let lastFocus = null;
  let isDragging = false;
  let dragStartX = 0;
  let dragStartY = 0;
  let startTranslateX = 0;
  let startTranslateY = 0;
  const pointers = new Map();
  let pinchStartDistance = 0;
  let pinchStartScale = 1;
  let pinchStartTranslateX = 0;
  let pinchStartTranslateY = 0;
  let pinchCenterX = 0;
  let pinchCenterY = 0;

  const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

  const applyTransform = () => {
    lightboxImage.style.transform = `translate(calc(-50% + ${translateX}px), calc(-50% + ${translateY}px)) scale(${scale})`;
    zoomReadout.textContent = `${Math.round(scale * 100)}%`;
  };

  const resetView = () => {
    scale = 1;
    translateX = 0;
    translateY = 0;
    applyTransform();
  };

  const zoomAt = (nextScale, clientX, clientY) => {
    const oldScale = scale;
    const rect = stage.getBoundingClientRect();
    const focusX = clientX - rect.left - rect.width / 2;
    const focusY = clientY - rect.top - rect.height / 2;
    scale = clamp(nextScale, MIN_SCALE, MAX_SCALE);
    const ratio = scale / oldScale;
    translateX = focusX - (focusX - translateX) * ratio;
    translateY = focusY - (focusY - translateY) * ratio;
    applyTransform();
  };

  const openLightbox = (trigger) => {
    const src = trigger.dataset.full;
    const title = trigger.dataset.title || trigger.querySelector("img")?.alt || "Screenshot preview";
    if (!src) return;
    lastFocus = document.activeElement;
    lightboxImage.src = src;
    lightboxImage.alt = `${title} screenshot preview`;
    if (lightboxTitle) lightboxTitle.textContent = title;
    resetView();
    lightbox.classList.add("is-open");
    lightbox.setAttribute("aria-hidden", "false");
    document.body.classList.add("lightbox-open");
    window.requestAnimationFrame(() => stage.focus({ preventScroll: true }));
  };

  const closeLightbox = () => {
    lightbox.classList.remove("is-open");
    lightbox.setAttribute("aria-hidden", "true");
    document.body.classList.remove("lightbox-open");
    pointers.clear();
    isDragging = false;
    if (lastFocus && typeof lastFocus.focus === "function") lastFocus.focus({ preventScroll: true });
  };

  triggers.forEach((trigger) => trigger.addEventListener("click", () => openLightbox(trigger)));

  lightbox.querySelectorAll("[data-lightbox-close]").forEach((button) => {
    button.addEventListener("click", closeLightbox);
  });

  lightbox.addEventListener("click", (event) => {
    if (event.target === lightbox) closeLightbox();
  });

  lightbox.querySelectorAll("[data-lightbox-zoom]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.lightboxZoom;
      const rect = stage.getBoundingClientRect();
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height / 2;
      if (action === "in") zoomAt(scale * 1.22, cx, cy);
      if (action === "out") zoomAt(scale / 1.22, cx, cy);
      if (action === "reset") resetView();
    });
  });

  stage.addEventListener("wheel", (event) => {
    if (!lightbox.classList.contains("is-open")) return;
    event.preventDefault();
    const delta = event.deltaY < 0 ? 1.12 : 1 / 1.12;
    zoomAt(scale * delta, event.clientX, event.clientY);
  }, { passive: false });

  const getDistance = (a, b) => Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
  const getCenter = (a, b) => ({ x: (a.clientX + b.clientX) / 2, y: (a.clientY + b.clientY) / 2 });

  stage.addEventListener("pointerdown", (event) => {
    if (!lightbox.classList.contains("is-open")) return;
    stage.setPointerCapture(event.pointerId);
    pointers.set(event.pointerId, event);

    if (pointers.size === 1) {
      isDragging = true;
      dragStartX = event.clientX;
      dragStartY = event.clientY;
      startTranslateX = translateX;
      startTranslateY = translateY;
    }

    if (pointers.size === 2) {
      const [first, second] = Array.from(pointers.values());
      pinchStartDistance = getDistance(first, second);
      pinchStartScale = scale;
      pinchStartTranslateX = translateX;
      pinchStartTranslateY = translateY;
      const center = getCenter(first, second);
      const rect = stage.getBoundingClientRect();
      pinchCenterX = center.x - rect.left - rect.width / 2;
      pinchCenterY = center.y - rect.top - rect.height / 2;
      isDragging = false;
    }
  });

  stage.addEventListener("pointermove", (event) => {
    if (!pointers.has(event.pointerId)) return;
    pointers.set(event.pointerId, event);

    if (pointers.size === 2) {
      const [first, second] = Array.from(pointers.values());
      const distance = getDistance(first, second);
      const oldScale = scale;
      scale = clamp(pinchStartScale * (distance / Math.max(1, pinchStartDistance)), MIN_SCALE, MAX_SCALE);
      const ratio = scale / Math.max(0.001, oldScale);
      translateX = pinchCenterX - (pinchCenterX - pinchStartTranslateX) * ratio;
      translateY = pinchCenterY - (pinchCenterY - pinchStartTranslateY) * ratio;
      applyTransform();
      return;
    }

    if (isDragging && pointers.size === 1) {
      translateX = startTranslateX + event.clientX - dragStartX;
      translateY = startTranslateY + event.clientY - dragStartY;
      applyTransform();
    }
  });

  const endPointer = (event) => {
    pointers.delete(event.pointerId);
    if (pointers.size === 0) isDragging = false;
  };

  stage.addEventListener("pointerup", endPointer);
  stage.addEventListener("pointercancel", endPointer);
  stage.addEventListener("lostpointercapture", endPointer);

  window.addEventListener("keydown", (event) => {
    if (!lightbox.classList.contains("is-open")) return;
    if (event.key === "Escape") closeLightbox();
    if ((event.key === "+" || event.key === "=") && !event.ctrlKey && !event.metaKey) {
      const rect = stage.getBoundingClientRect();
      zoomAt(scale * 1.18, rect.left + rect.width / 2, rect.top + rect.height / 2);
    }
    if (event.key === "-" && !event.ctrlKey && !event.metaKey) {
      const rect = stage.getBoundingClientRect();
      zoomAt(scale / 1.18, rect.left + rect.width / 2, rect.top + rect.height / 2);
    }
    if (event.key === "0") resetView();
  });

  const supportsFinePointer = window.matchMedia("(pointer: fine)").matches;
  const createClickRipple = (event) => {
    if (reduceMotion || event.button !== 0 || !supportsFinePointer) return;
    if (event.target.closest(".lightbox-stage")) return;

    addLiquidImpulse(event.clientX, event.clientY, 1.15);

    const effect = document.createElement("span");
    effect.className = "click-ripple";
    effect.style.setProperty("--x", `${event.clientX}px`);
    effect.style.setProperty("--y", `${event.clientY}px`);

    document.body.appendChild(effect);
    effect.addEventListener("animationend", () => effect.remove(), { once: true });
    window.setTimeout(() => effect.remove(), 520);
  };

  window.addEventListener("pointerdown", createClickRipple, { passive: true });

})();
