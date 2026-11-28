import * as React from "react"
import { Button as ButtonPrimitive } from "@base-ui/react/button"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "cn"

import { Spinner } from "@/components/ui/spinner"

const buttonVariants = cva(
  "group/button relative isolate inline-flex shrink-0 items-center justify-center rounded-lg border border-transparent bg-clip-padding text-sm font-medium whitespace-nowrap select-none cursor-pointer outline-none transition-[box-shadow,color,background-color,border-color,opacity,transform,scale] duration-[var(--cds-dur-slow)] [transition-timing-function:ease-out,ease-out,ease-out,ease-out,ease-out,var(--cds-btn-spring),var(--cds-btn-spring)] active:not-disabled:not-[[aria-haspopup]]:not-data-[variant=link]:scale-[0.975] active:not-disabled:not-[[aria-haspopup]]:not-data-[variant=link]:duration-[var(--cds-dur-fast)] active:not-disabled:not-[[aria-haspopup]]:not-data-[variant=link]:ease-out in-data-[slot=button-group]:active:scale-100! in-data-[slot=button-group]:transform-none motion-reduce:transition-none motion-reduce:active:scale-100! motion-reduce:active:transform-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:cursor-default aria-disabled:pointer-events-none aria-disabled:cursor-default data-[disabled]:cursor-default [&:disabled:not([aria-busy])]:opacity-[var(--cds-opacity-disabled,0.45)] aria-busy:pointer-events-none aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default:
          "bg-primary text-primary-foreground hover:bg-primary/85 aria-expanded:bg-primary/85",
        outline:
          "border-border bg-background hover:bg-accent hover:text-accent-foreground aria-expanded:bg-accent aria-expanded:text-accent-foreground dark:border-input dark:bg-input/30 dark:hover:bg-input/50",
        secondary:
          "bg-secondary text-secondary-foreground hover:bg-[color-mix(in_oklch,var(--secondary),var(--secondary-foreground)_12%)] aria-expanded:bg-[color-mix(in_oklch,var(--secondary),var(--secondary-foreground)_12%)] aria-expanded:text-secondary-foreground",
        ghost:
          "hover:bg-muted hover:text-foreground aria-expanded:bg-muted aria-expanded:text-foreground aria-pressed:bg-muted dark:hover:bg-muted/50",
        destructive:
          "bg-destructive/10 text-destructive hover:bg-destructive/20 aria-expanded:bg-destructive/20 focus-visible:border-destructive/40 focus-visible:ring-destructive/20 dark:bg-destructive/20 dark:hover:bg-destructive/30 dark:focus-visible:ring-destructive/40",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        default:
          "h-8 gap-1.5 px-3 text-sm has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2",
        xs: "h-6 [@media(pointer:coarse),(hover:none)]:h-8 gap-1 rounded-[6px] px-2 text-xs in-data-[slot=button-group]:rounded-lg has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 [&_svg:not([class*='size-'])]:size-3.5",
        sm: "h-7 gap-1.5 rounded-lg px-2.5 text-[0.8125rem] in-data-[slot=button-group]:rounded-lg has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2 [&_svg:not([class*='size-'])]:size-3.5",
        lg: "h-10 gap-1.5 rounded-[10px] px-4 text-sm has-data-[icon=inline-end]:pr-3 has-data-[icon=inline-start]:pl-3",
        // Nút chính trên trang chủ và trên điện thoại: cao 44px cho dễ bấm
        xl: "h-11 gap-2 rounded-[10px] px-5 text-[15px] has-data-[icon=inline-end]:pr-4 has-data-[icon=inline-start]:pl-4",
        icon: "size-8 aspect-square px-0",
        "icon-xs":
          "size-6 [@media(pointer:coarse),(hover:none)]:size-8 aspect-square px-0 rounded-[6px] in-data-[slot=button-group]:rounded-lg [&_svg:not([class*='size-'])]:size-3.5",
        "icon-sm":
          "size-7 aspect-square px-0 rounded-lg in-data-[slot=button-group]:rounded-lg [&_svg:not([class*='size-'])]:size-4",
        "icon-lg": "size-10 aspect-square px-0 rounded-[10px]",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

function resolveNativeButton(
  render: ButtonPrimitive.Props["render"],
  explicitNativeButton: boolean | undefined
): boolean | undefined {
  if (explicitNativeButton !== undefined) return explicitNativeButton
  if (!React.isValidElement(render)) return undefined
  if (render.type === "button") return true
  if (render.type === "a" || render.type === "span" || render.type === "div") {
    return false
  }
  const renderProps = render.props as Record<string, unknown> | null
  if (renderProps && "href" in renderProps) return false
  return undefined
}

interface ButtonProps
  extends ButtonPrimitive.Props,
    VariantProps<typeof buttonVariants> {
  loading?: boolean
}

function Button({
  className,
  variant = "default",
  size = "default",
  loading = false,
  disabled,
  focusableWhenDisabled,
  nativeButton,
  children,
  ...props
}: ButtonProps) {
  const isBusy =
    loading || props["aria-busy"] === true || props["aria-busy"] === "true"

  const isIcon =
    size === "icon" ||
    size === "icon-xs" ||
    size === "icon-sm" ||
    size === "icon-lg"

  return (
    <ButtonPrimitive
      data-slot="button"
      data-variant={variant ?? "default"}
      data-size={size ?? "default"}
      data-icon-only={isIcon ? "" : undefined}
      nativeButton={resolveNativeButton(props.render, nativeButton)}
      disabled={disabled || isBusy}
      focusableWhenDisabled={focusableWhenDisabled ?? (isBusy ? true : undefined)}
      aria-busy={isBusy ? true : undefined}
      aria-disabled={isBusy ? true : props["aria-disabled"] || disabled}
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    >
      <span
        className={cn(
          "inline-flex min-w-0 w-full gap-[inherit] [align-items:inherit] [justify-content:inherit] [flex-direction:inherit]",
          isBusy && "opacity-0"
        )}
      >
        {children}
      </span>
      {isBusy && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 grid place-items-center text-current"
        >
          <Spinner size={size === "xs" || size === "icon-xs" ? "xs" : "sm"} />
        </span>
      )}
    </ButtonPrimitive>
  )
}

export { Button, buttonVariants, type ButtonProps }
