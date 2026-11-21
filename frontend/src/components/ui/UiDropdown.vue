<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'

// A menu button: Enter, Space or ArrowDown opens it; the arrows move between items; Esc and Tab close it (ТЗ 10).
const props = withDefaults(defineProps<{ label: string; align?: 'start' | 'end' }>(), { align: 'end' })
const open = ref(false)
const root = ref<HTMLElement>()
const menu = ref<HTMLElement>()
const id = `menu-${useId()}`

function items() {
  return [...(menu.value?.querySelectorAll<HTMLElement>('[role=menuitem], [role=menuitemradio]:not([disabled])') ?? [])]
}
function focusItem(index: number) {
  const list = items()
  list[(index + list.length) % list.length]?.focus()
}
async function show(focus: 'first' | 'last' | null = 'first') {
  open.value = true
  await nextTick()
  if (focus) focusItem(focus === 'first' ? 0 : -1)
}
function hide(returnFocus = true) {
  if (!open.value) return
  open.value = false
  if (returnFocus) root.value?.querySelector<HTMLElement>('[aria-haspopup]')?.focus()
}
function toggle() {
  if (open.value) hide()
  else void show(null)
}
function onTriggerKey(event: KeyboardEvent) {
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    void show(event.key === 'ArrowDown' ? 'first' : 'last')
  }
}
function onMenuKey(event: KeyboardEvent) {
  const list = items()
  const index = list.indexOf(document.activeElement as HTMLElement)
  const moves: Record<string, number> = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: list.length - 1 }
  if (event.key in moves) { event.preventDefault(); focusItem(moves[event.key]) }
  else if (event.key === 'Escape') { event.preventDefault(); hide() }
  else if (event.key === 'Tab') hide(false)
}
function onMenuClick(event: MouseEvent) {
  if ((event.target as HTMLElement).closest('[role=menuitem], [role=menuitemradio]')) hide()
}
function onOutside(event: PointerEvent) {
  if (!root.value?.contains(event.target as Node)) hide(false)
}
watch(open, (value) => {
  if (value) document.addEventListener('pointerdown', onOutside)
  else document.removeEventListener('pointerdown', onOutside)
})
onBeforeUnmount(() => document.removeEventListener('pointerdown', onOutside))
defineExpose({ hide, align: props.align })
</script>

<template>
  <div ref="root" class="ui-dropdown">
    <slot name="trigger" :open="open" :toggle="toggle" :attrs="{
      'aria-haspopup': 'menu' as const, 'aria-expanded': open, 'aria-controls': id, 'aria-label': label,
      onClick: toggle, onKeydown: onTriggerKey }" />
    <Transition name="ui-pop">
      <div v-if="open" :id="id" ref="menu" class="menu" :class="align" role="menu" :aria-label="label"
        @keydown="onMenuKey" @click="onMenuClick">
        <slot :close="hide" />
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.ui-dropdown { position: relative; display: inline-flex; }
.menu {
  position: absolute; z-index: 40; top: calc(100% + 6px); display: grid; min-width: 13rem; padding: var(--space-1);
  border: 1px solid var(--border); border-radius: var(--radius-lg); background-color: var(--surface-1);
  box-shadow: var(--elevation-3); transform-origin: top right;
}
.menu.start { left: 0; transform-origin: top left; }
.menu.end { right: 0; }
.menu :deep(:is([role=menuitem], [role=menuitemradio])) {
  display: flex; align-items: center; gap: .6rem; width: 100%; min-height: 38px; padding: 0 var(--space-3);
  border: 0; border-radius: var(--radius-sm); color: var(--text); background-color: transparent;
  font-weight: 550; font-size: var(--font-size-sm); text-align: left; text-decoration: none;
  transition: background-color var(--motion-fast), color var(--motion-fast);
}
.menu :deep(:is([role=menuitem], [role=menuitemradio]) svg) { width: 17px; height: 17px; color: var(--text-muted); }
.menu :deep(:is([role=menuitem], [role=menuitemradio]):is(:hover, :focus-visible)) { background-color: var(--surface-2); outline: none; }
.menu :deep(:is([role=menuitem], [role=menuitemradio]):focus-visible) { box-shadow: inset 0 0 0 2px var(--focus); }
.menu :deep([role=menuitemradio][aria-checked=true]) { color: var(--accent); font-weight: 700; }
.menu :deep([role=menuitem].danger) { color: var(--danger); }
.menu :deep([role=separator]) { height: 1px; margin: var(--space-1) 0; background-color: var(--border); }
.ui-pop-enter-active, .ui-pop-leave-active { transition: opacity var(--motion-fast) var(--ease-out), transform var(--motion-fast) var(--ease-out); }
.ui-pop-enter-from, .ui-pop-leave-to { opacity: 0; transform: translateY(-4px) scale(.98); }
</style>
