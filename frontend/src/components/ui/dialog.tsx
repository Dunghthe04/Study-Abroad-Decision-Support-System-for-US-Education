"use client"

import * as React from "react"
import { Dialog as DialogPrimitive } from "@base-ui/react/dialog"
import { Button as ButtonPrimitive } from "@base-ui/react/button"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "cn"

import { Spinner } from "@/components/ui/spinner"

export type DialogVariant = "action" | "form"
export type DialogSize = "sm" | "md" | "lg" | "xl" | "2xl" | "full"
export type DialogFooterLayout = "pair" | "single" | "stack" | "split-top"
export type DialogIconTone =
  | "default"
  | "brand"
  | "destructive"
  | "warning"
  | "success"
  | "muted"
export type DialogActionTone =
  | "tint"
  | "ghost"
  | "primary"
  | "primary-tint"
  | "destructive"
  | "destructive-tint"
  | "ok"

const DialogVariantContext = React.createContext<DialogVariant>("form")

export const dialogSizeClasses: Record<DialogSize, string> = {
  sm: "sm:max-w-[360px]",
  md: "sm:max-w-[440px]",
  lg: "sm:max-w-[540px]",
  xl: "sm:max-w-[680px]",
  "2xl": "sm:max-w-[820px]",
  full: "sm:max-w-[min(68rem,calc(100vw-3rem))]",
}

export const dialogIconVariants = cva(
  "inline-flex shrink-0 items-center justify-center bg-transparent py-3 [&_svg]:size-[38px] [&_svg]:stroke-[1.65]",
  {
    variants: {
      tone: {
        default: "text-ink",
        brand: "text-brand",
        destructive: "text-risk",
        warning: "text-warn",
        success: "text-ok",
        muted: "text-ink-2",
      },
    },
    defaultVariants: {
      tone: "brand",
    },
  }
)

export const dialogActionVariants = cva(
  "relative isolate inline-flex h-10 w-full shrink-0 items-center justify-center rounded-[10px] border-0 bg-clip-padding px-4 text-sm font-medium whitespace-nowrap select-none cursor-pointer outline-none transition-[box-shadow,color,background-color,opacity,transform,scale] duration-[var(--cds-dur-slow)] ease-[var(--cds-btn-spring)] active:not-disabled:scale-[0.975] active:not-disabled:duration-[var(--cds-dur-fast)] active:not-disabled:ease-out motion-reduce:transition-none motion-reduce:active:scale-100! motion-reduce:active:transform-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:cursor-default aria-disabled:pointer-events-none aria-disabled:cursor-default [&:disabled:not([aria-busy])]:opacity-[var(--cds-opacity-disabled,0.45)] aria-busy:pointer-events-none",
  {
    variants: {
      tone: {
        tint: "bg-surface-2 text-ink hover:bg-surface-3 active:bg-surface-3",
        ghost: "bg-transparent text-ink-2 hover:bg-surface-2 hover:text-ink",
        primary: "bg-brand text-brand-foreground hover:bg-brand/90",
        "primary-tint":
          "bg-brand-wash text-brand-ink hover:bg-[color-mix(in_oklch,var(--brand-wash),var(--brand-ink)_10%)]",
        destructive:
          "bg-risk text-white hover:bg-risk/90 focus-visible:ring-risk/30",
        "destructive-tint":
          "bg-risk-wash text-risk hover:bg-[color-mix(in_oklch,var(--risk-wash),var(--risk)_12%)] focus-visible:ring-risk/30",
        ok: "bg-ok text-white hover:bg-ok/90 focus-visible:ring-ok/30",
      },
    },
    defaultVariants: {
      tone: "primary",
    },
  }
)

export const dialogFooterVariants = cva("shrink-0 w-full pt-3", {
  variants: {
    layout: {
      pair: "grid grid-cols-2 gap-1.5",
      single: "grid grid-cols-1",
      stack: "flex flex-col gap-1.5",
      "split-top": "grid grid-cols-2 gap-1.5 [&>*:first-child]:col-span-2",
    },
  },
  defaultVariants: {
    layout: "pair",
  },
})

export const dialogBodyClassName =
  "min-h-0 flex-1 overflow-y-auto -mx-1 px-1 py-0.5 text-sm text-ink " +
  "[&_[data-slot=input]]:h-9 [&_[data-slot=input]]:rounded-lg [&_[data-slot=input]]:px-3 [&_[data-slot=input]]:text-sm " +
  "[&_[data-slot=textarea]]:rounded-lg [&_[data-slot=textarea]]:px-3 [&_[data-slot=textarea]]:py-2 [&_[data-slot=textarea]]:text-sm " +
  "[&_[data-slot=select-trigger][data-size=default]]:h-9 [&_[data-slot=select-trigger][data-size=default]]:rounded-lg [&_[data-slot=select-trigger][data-size=default]]:px-3 [&_[data-slot=select-trigger][data-size=default]]:text-sm " +
  "[&_[data-slot=field-label]]:text-[0.8125rem] [&_[data-slot=field-label]]:font-medium [&_[data-slot=field-label]]:text-ink"

export const dialogOverlayClassName =
  "fixed inset-0 isolate z-50 bg-ink/30 duration-100 supports-backdrop-filter:backdrop-blur-xs data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0"

export const dialogPopupBaseClassName =
  "fixed top-1/2 left-1/2 z-50 flex max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-[18px] bg-surface px-5 pt-7 pb-5 text-sm text-ink shadow-[0_20px_50px_-14px_rgba(15,34,51,0.2),0_4px_14px_-4px_rgba(15,34,51,0.08)] ring-1 ring-ink/[0.08] duration-100 outline-none sm:px-6 sm:pt-8 sm:pb-6 data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95 data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95"

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

function Dialog({ ...props }: DialogPrimitive.Root.Props) {
  return <DialogPrimitive.Root data-slot="dialog" {...props} />
}

function DialogTrigger({ className, ...props }: DialogPrimitive.Trigger.Props) {
  return (
    <DialogPrimitive.Trigger
      data-slot="dialog-trigger"
      className={cn("cursor-pointer disabled:cursor-default", className)}
      {...props}
    />
  )
}

function DialogPortal({ ...props }: DialogPrimitive.Portal.Props) {
  return <DialogPrimitive.Portal data-slot="dialog-portal" {...props} />
}

function DialogClose({ ...props }: DialogPrimitive.Close.Props) {
  return <DialogPrimitive.Close data-slot="dialog-close" {...props} />
}

function DialogOverlay({
  className,
  ...props
}: DialogPrimitive.Backdrop.Props) {
  return (
    <DialogPrimitive.Backdrop
      data-slot="dialog-overlay"
      className={cn(dialogOverlayClassName, className)}
      {...props}
    />
  )
}

function DialogContent({
  className,
  children,
  variant = "form",
  size,
  ...props
}: DialogPrimitive.Popup.Props & {
  variant?: DialogVariant
  size?: DialogSize
}) {
  const resolvedSize: DialogSize =
    size ?? (variant === "action" ? "sm" : "md")

  return (
    <DialogPortal>
      <DialogOverlay />
      <DialogPrimitive.Popup
        data-slot="dialog-content"
        data-variant={variant}
        data-size={resolvedSize}
        className={cn(
          dialogPopupBaseClassName,
          dialogSizeClasses[resolvedSize],
          className
        )}
        {...props}
      >
        <DialogVariantContext.Provider value={variant}>
          {children}
        </DialogVariantContext.Provider>
      </DialogPrimitive.Popup>
    </DialogPortal>
  )
}

function DialogIcon({
  className,
  tone = "brand",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof dialogIconVariants>) {
  const variant = React.useContext(DialogVariantContext)
  return (
    <div
      data-slot="dialog-icon"
      aria-hidden="true"
      className={cn(
        dialogIconVariants({ tone }),
        variant === "action" ? "mx-auto" : "self-start",
        className
      )}
      {...props}
    />
  )
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  const variant = React.useContext(DialogVariantContext)
  return (
    <div
      data-slot="dialog-header"
      className={cn(
        "flex shrink-0 flex-col gap-1.5",
        variant === "action"
          ? "items-center text-center"
          : "items-start text-left",
        className
      )}
      {...props}
    />
  )
}

function DialogTitle({ className, ...props }: DialogPrimitive.Title.Props) {
  return (
    <DialogPrimitive.Title
      data-slot="dialog-title"
      className={cn(
        "font-heading text-[1.0625rem] font-semibold leading-snug tracking-[-0.01em] text-ink sm:text-lg",
        className
      )}
      {...props}
    />
  )
}

function DialogDescription({
  className,
  ...props
}: DialogPrimitive.Description.Props) {
  const variant = React.useContext(DialogVariantContext)
  return (
    <DialogPrimitive.Description
      data-slot="dialog-description"
      className={cn(
        "text-sm leading-relaxed text-ink-2 *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-ink",
        variant === "action" ? "text-balance" : "text-pretty",
        className
      )}
      {...props}
    />
  )
}

function DialogBody({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-body"
      className={cn(dialogBodyClassName, className)}
      {...props}
    />
  )
}

function DialogFooter({
  className,
  layout = "pair",
  ...props
}: React.ComponentProps<"div"> & VariantProps<typeof dialogFooterVariants>) {
  return (
    <div
      data-slot="dialog-footer"
      data-layout={layout}
      className={cn(dialogFooterVariants({ layout }), className)}
      {...props}
    />
  )
}

export interface DialogActionProps
  extends ButtonPrimitive.Props,
    VariantProps<typeof dialogActionVariants> {
  loading?: boolean
}

function DialogAction({
  className,
  tone = "primary",
  loading = false,
  disabled,
  nativeButton,
  type = "button",
  children,
  ...props
}: DialogActionProps) {
  const isBusy =
    loading || props["aria-busy"] === true || props["aria-busy"] === "true"
  const resolvedNative = resolveNativeButton(props.render, nativeButton)

  return (
    <ButtonPrimitive
      data-slot="dialog-action"
      data-tone={tone}
      type={resolvedNative === false ? undefined : type}
      nativeButton={resolvedNative}
      disabled={disabled || isBusy}
      focusableWhenDisabled={isBusy ? true : undefined}
      aria-busy={isBusy ? true : undefined}
      aria-disabled={isBusy ? true : props["aria-disabled"] || disabled}
      className={cn(dialogActionVariants({ tone }), className)}
      {...props}
    >
      <span
        className={cn(
          "inline-flex w-full min-w-0 items-center justify-center truncate",
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
          <Spinner size="sm" />
        </span>
      )}
    </ButtonPrimitive>
  )
}

export interface DialogCancelProps
  extends Omit<DialogPrimitive.Close.Props, "render"> {
  tone?: "tint" | "ghost"
  loading?: boolean
  /** Use DialogPrimitive.Close automatically unless controlled externally. */
  asClose?: boolean
}

function DialogCancel({
  className,
  tone = "tint",
  loading = false,
  asClose = true,
  ...props
}: DialogCancelProps) {
  if (!asClose) {
    return (
      <DialogAction
        data-slot="dialog-cancel"
        tone={tone}
        loading={loading}
        type="button"
        className={className}
        {...(props as DialogActionProps)}
      />
    )
  }

  return (
    <DialogPrimitive.Close
      data-slot="dialog-cancel"
      render={
        <DialogAction
          tone={tone}
          loading={loading}
          type="button"
          className={className}
        />
      }
      {...props}
    />
  )
}

export interface AppDialogProps {
  open?: boolean
  defaultOpen?: boolean
  onOpenChange?: (open: boolean) => void
  /** Optional trigger element (renders as DialogTrigger). */
  trigger?: React.ReactElement
  /** Optional simple trigger button label when no custom `trigger` element is provided. */
  triggerLabel?: string
  variant?: DialogVariant
  size?: DialogSize
  icon?: React.ReactNode
  iconTone?: DialogIconTone
  iconClassName?: string
  title: React.ReactNode
  description?: React.ReactNode
  children?: React.ReactNode
  actionsLayout?: DialogFooterLayout
  /** Cancel/close button text. Pass `null` to omit. */
  cancelLabel?: string | null
  cancelTone?: "tint" | "ghost"
  onCancel?: () => void
  confirmLabel?: string
  confirmTone?: DialogActionTone
  confirmDisabled?: boolean
  onConfirm?: () => void
  /** Form submit handler; wraps body & footer in `<form>` and sets confirm button to `type="submit"`. */
  onSubmit?: React.FormEventHandler<HTMLFormElement>
  loading?: boolean
  /** Allow backdrop click / Escape close. Defaults to `true` (automatically blocked while `loading`). */
  dismissible?: boolean
  /** Custom footer replacing the default cancel/confirm buttons (use `DialogFooter` + `DialogAction`/`DialogCancel`). */
  footer?: React.ReactNode
  contentClassName?: string
  bodyClassName?: string
}

function AppDialog({
  open,
  defaultOpen,
  onOpenChange,
  trigger,
  triggerLabel,
  variant = "action",
  size,
  icon,
  iconTone = "brand",
  iconClassName,
  title,
  description,
  children,
  actionsLayout,
  cancelLabel,
  cancelTone = "tint",
  onCancel,
  confirmLabel,
  confirmTone = "primary",
  confirmDisabled = false,
  onConfirm,
  onSubmit,
  loading = false,
  dismissible = true,
  footer,
  contentClassName,
  bodyClassName,
}: AppDialogProps) {
  const canDismiss = dismissible && !loading
  const resolvedCancelLabel =
    cancelLabel === undefined ? (confirmLabel ? "Hủy" : "Đóng") : cancelLabel
  const hasCancel = resolvedCancelLabel !== null
  const hasConfirm = Boolean(confirmLabel)
  const resolvedLayout: DialogFooterLayout =
    actionsLayout ?? (hasCancel && hasConfirm ? "pair" : "single")

  const handleOpenChange = (next: boolean) => {
    if (!next && !canDismiss) return
    onOpenChange?.(next)
  }

  const headerNode = (
    <DialogHeader>
      {icon && (
        <DialogIcon tone={iconTone} className={iconClassName}>
          {icon}
        </DialogIcon>
      )}
      <DialogTitle>{title}</DialogTitle>
      {description && <DialogDescription>{description}</DialogDescription>}
    </DialogHeader>
  )

  const bodyNode = children ? (
    <DialogBody className={bodyClassName}>{children}</DialogBody>
  ) : null

  const footerNode =
    footer !== undefined ? (
      footer
    ) : hasCancel || hasConfirm ? (
      <DialogFooter layout={resolvedLayout}>
        {hasCancel && (
          <DialogCancel
            tone={cancelTone}
            disabled={loading}
            onClick={onCancel}
          >
            {resolvedCancelLabel}
          </DialogCancel>
        )}
        {hasConfirm && (
          <DialogAction
            tone={confirmTone}
            type={onSubmit ? "submit" : "button"}
            disabled={confirmDisabled}
            loading={loading}
            onClick={onSubmit ? undefined : onConfirm}
          >
            {confirmLabel}
          </DialogAction>
        )}
      </DialogFooter>
    ) : null

  return (
    <Dialog
      open={open}
      defaultOpen={defaultOpen}
      onOpenChange={handleOpenChange}
      disablePointerDismissal={!canDismiss}
    >
      {trigger ? (
        <DialogTrigger render={trigger} />
      ) : triggerLabel ? (
        <DialogTrigger
          render={
            <DialogAction tone="primary" className="w-auto">
              {triggerLabel}
            </DialogAction>
          }
        />
      ) : null}

      <DialogContent
        variant={variant}
        size={size}
        className={contentClassName}
      >
        {onSubmit ? (
          <form
            onSubmit={onSubmit}
            noValidate
            className="flex min-h-0 flex-1 flex-col gap-4"
          >
            {headerNode}
            {bodyNode}
            {footerNode}
          </form>
        ) : (
          <>
            {headerNode}
            {bodyNode}
            {footerNode}
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}

export {
  AppDialog,
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogOverlay,
  DialogPortal,
  DialogTitle,
  DialogTrigger,
}
