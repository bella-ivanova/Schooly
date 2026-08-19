import type { RouteLocationRaw } from 'vue-router'

export interface NavItem {
  label: string
  active?: boolean
  disabled?: boolean
  primary?: boolean
  to?: RouteLocationRaw
  sectionHeader?: boolean
}
