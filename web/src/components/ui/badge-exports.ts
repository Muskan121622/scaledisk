// Convenience re-exports so imports across screens read like `@/components/ui/badge`.
// Splitting these into dedicated files is what shadcn does; keeping them combined here
// avoids a bunch of near-identical file writes for a handful of primitives.
export { Badge, badgeVariants } from './label-badge';
export { Label } from './label-badge';
