import { cn } from "cn"
import { cva, type VariantProps } from "class-variance-authority"
import { Loader2Icon } from "lucide-react"

const spinnerVariants = cva("animate-spin", {
  variants: {
    size: {
      xs: "size-3",
      sm: "size-3.5",
      default: "size-4",
      lg: "size-5",
    },
  },
  defaultVariants: {
    size: "default",
  },
})

function Spinner({
  className,
  size = "default",
  ...props
}: React.ComponentProps<"svg"> & VariantProps<typeof spinnerVariants>) {
  return (
    <Loader2Icon
      data-slot="spinner"
      role="status"
      aria-label="Loading"
      className={cn(spinnerVariants({ size }), className)}
      {...props}
    />
  )
}

export { Spinner, spinnerVariants }
