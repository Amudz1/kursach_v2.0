import { managedAttributes, managedVars, toRootAttributes, type AppearanceSettings } from './model'

/** Writes the settings to `<html>` and removes attributes and properties left over from earlier settings. */
export function applyAppearance(settings: AppearanceSettings, root: HTMLElement = document.documentElement) {
  const { dataset, vars } = toRootAttributes(settings)
  for (const name of managedAttributes) {
    const value = dataset[name]
    if (value === undefined) root.removeAttribute(`data-${name}`)
    else if (root.getAttribute(`data-${name}`) !== value) root.setAttribute(`data-${name}`, value)
  }
  for (const name of managedVars) {
    const value = vars[name]
    if (value === undefined) root.style.removeProperty(`--${name}`)
    else root.style.setProperty(`--${name}`, value)
  }
}
