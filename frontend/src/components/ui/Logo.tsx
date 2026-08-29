export function Logo({ className = 'h-8 w-8' }: { className?: string }) {
  return <img src="/logo-mark.png" alt="The Shop Keeper" className={`${className} rounded-lg`} />
}
