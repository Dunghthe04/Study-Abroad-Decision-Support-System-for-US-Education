import { mergeProps } from "@base-ui/react/merge-props"
import { useRender } from "@base-ui/react/use-render"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "cn"

import { Separator } from "@/components/ui/separator"

const buttonGroupVariants = cva(
  "flex w-fit items-stretch *:focus-visible:relative *:focus-visible:z-10 has-[>[data-slot=button-group]]:gap-2 has-[select[aria-hidden=true]:last-child]:[&>[data-slot=select-trigger]:last-of-type]:rounded-r-lg [&>[data-slot=select-trigger]:not([class*='w-'])]:w-fit [&>input]:flex-1",
  {
    variants: {
      orientation: {
        horizontal:
          "*:data-slot:rounded-r-none [&>[data-slot]:not(:has(~[data-slot]))]:rounded-r-lg! [&>[data-slot]~[data-slot]]:rounded-l-none [&>[data-slot]~[data-slot]]:border-l-0",
        vertical:
          "flex-col *:data-slot:rounded-b-none [&>[data-slot]:not(:has(~[data-slot]))]:rounded-b-lg! [&>[data-slot]~[data-slot]]:rounded-t-none [&>[data-slot]~[data-slot]]:border-t-0",
      },
    },
    defaultVariants: {
      orientation: "horizontal",
    },
  }
)

function ButtonGroup({
  className,
  orientation,
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof buttonGroupVariants>) {
  return (
    <div
      role="group"
      data-slot="button-group"
      data-orientation={orientation}
      className={cn(buttonGroupVariants({ orientation }), className)}
      {...props}
    />
  )
}

function ButtonGroupText({
  className,
  render,
  ...props
}: useRender.ComponentProps<"div">) {
  return useRender({
    defaultTagName: "div",
    props: mergeProps<"div">(
      {
        className: cn(
          "flex items-center gap-1.5 rounded-lg border bg-muted px-3 text-sm font-medium [&_svg]:pointer-events-none [&_svg:not([class*='size-'])]:size-4",
          className
        ),
      },
      props
    ),
    render,
    state: {
      slot: "button-group-text",
    },
  })
}

function ButtonGroupSeparator({
  className,
  orientation = "vertical",
  ...props
}: React.ComponentProps<typeof Separator>) {
  return (
    <Separator
      data-slot="button-group-separator"
      orientation={orientation}
      className={cn(
        "relative self-stretch bg-input data-horizontal:mx-px data-horizontal:w-auto data-vertical:my-px data-vertical:h-auto",
        className
      )}
      {...props}
    />
  )
}

/* Khuôn SplitDropdownButton theo claude-ui: đường chia 50% chiều cao tự ẩn khi hover/mở menu */
function SplitButton({
  className,
  children,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      role="group"
      data-slot="button-group"
      data-cds="SplitDropdownButton"
      className={cn(
        "group/split relative inline-flex w-fit shrink-0 items-stretch rounded-lg *:focus-visible:relative *:focus-visible:z-10",
        className
      )}
      {...props}
    >
      {children}
    </div>
  )
}

function SplitButtonSegment({
  className,
  children,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div data-cds-segment className={cn("contents", className)} {...props}>
      {children}
    </div>
  )
}

function SplitButtonSeparator({
  className,
  ...props
}: React.ComponentProps<"span">) {
  return (
    <span
      aria-hidden="true"
      data-slot="split-button-separator"
      className={cn(
        "relative w-px shrink-0 self-stretch bg-border/40 transition-opacity duration-[var(--cds-dur-fast)]",
        "[[data-cds-segment]:hover+&>[data-cds-seam-line]]:opacity-0",
        "[&:has(+[data-cds-segment]:hover)>[data-cds-seam-line]]:opacity-0",
        "[[data-cds-segment]:has([aria-haspopup][aria-expanded=true])+&>[data-cds-seam-line]]:opacity-0",
        "[&:has(+[data-cds-segment]_[aria-haspopup][aria-expanded=true])>[data-cds-seam-line]]:opacity-0",
        "[[data-cds-segment]:has([aria-pressed=true])+&>[data-cds-seam-line]]:opacity-0",
        "[&:has(+[data-cds-segment]_[aria-pressed=true])>[data-cds-seam-line]]:opacity-0",
        className
      )}
      {...props}
    >
      <span
        data-cds-seam-line
        className="absolute inset-x-0 inset-y-1/4 bg-border transition-opacity duration-[var(--cds-dur-fast)]"
      />
    </span>
  )
}

const SplitDropdownButton = SplitButton

export {
  ButtonGroup,
  ButtonGroupSeparator,
  ButtonGroupText,
  buttonGroupVariants,
  SplitButton,
  SplitButtonSegment,
  SplitButtonSeparator,
  SplitDropdownButton,
}
