import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface GradientTextProps {
  children: ReactNode;
  className?: string;
  variant?: 'cool' | 'warm' | 'ocean';
}

const gradients = {
  cool: 'linear-gradient(120deg, #8b5cf6 0%, #a855f7 50%, #6366f1 100%)',
  warm: 'linear-gradient(120deg, #c084fc 0%, #ec4899 55%, #8b5cf6 100%)',
  ocean: 'linear-gradient(120deg, #6366f1 0%, #22d3ee 50%, #a855f7 100%)',
};

export function GradientText({ children, className, variant = 'cool' }: GradientTextProps) {
  return (
    <span
      className={cn('bg-clip-text text-transparent', className)}
      style={{ backgroundImage: gradients[variant] }}
    >
      {children}
    </span>
  );
}
