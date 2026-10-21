import Tilt from 'react-parallax-tilt';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface TiltCardProps {
  children: ReactNode;
  className?: string;
  glare?: boolean;
  maxTilt?: number;
  scale?: number;
  perspective?: number;
  glowColor?: string;
}

export function TiltCard({
  children,
  className,
  glare = true,
  maxTilt = 12,
  scale = 1.03,
  perspective = 1000,
  glowColor = 'rgba(139, 92, 246, 0.14)',
}: TiltCardProps) {
  return (
    <Tilt
      glareEnable={glare}
      glareMaxOpacity={0.15}
      glareColor="#ffffff"
      glarePosition="all"
      glareBorderRadius="16px"
      tiltMaxAngleX={maxTilt}
      tiltMaxAngleY={maxTilt}
      scale={scale}
      perspective={perspective}
      transitionSpeed={600}
      className={cn('rounded-2xl', className)}
      style={{ transformStyle: 'preserve-3d' }}
    >
      <div
        className="surface rounded-2xl p-6 transition-shadow duration-300 hover:shadow-[0_10px_36px_rgba(139,92,246,0.16)]"
        style={{ '--tilt-glow': glowColor } as React.CSSProperties}
      >
        {children}
      </div>
    </Tilt>
  );
}
