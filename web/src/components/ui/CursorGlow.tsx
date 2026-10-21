import { useEffect, useRef } from 'react';

export function CursorGlow() {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    let raf: number;
    let x = -400, y = -400;
    let targetX = -400, targetY = -400;

    function onMouseMove(e: MouseEvent) {
      targetX = e.clientX;
      targetY = e.clientY;
    }

    function animate() {
      x += (targetX - x) * 0.08;
      y += (targetY - y) * 0.08;
      el!.style.transform = `translate(${x - 200}px, ${y - 200}px)`;
      raf = requestAnimationFrame(animate);
    }

    document.addEventListener('mousemove', onMouseMove);
    raf = requestAnimationFrame(animate);

    return () => {
      document.removeEventListener('mousemove', onMouseMove);
      cancelAnimationFrame(raf);
    };
  }, []);

  return (
    <div
      ref={ref}
      className="pointer-events-none fixed left-0 top-0 z-0 h-[320px] w-[320px] rounded-full opacity-50"
      style={{
        background: 'radial-gradient(circle, rgba(139, 92, 246, 0.12) 0%, rgba(99, 102, 241, 0.07) 45%, transparent 70%)',
        willChange: 'transform',
      }}
    />
  );
}
