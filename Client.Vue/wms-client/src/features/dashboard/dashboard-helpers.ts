import type { ApexOptions } from 'apexcharts'

export const WINDOW_OPTIONS = [
  { label: 'Last 7 days', value: 7 },
  { label: 'Last 14 days', value: 14 },
  { label: 'Last 30 days', value: 30 },
  { label: 'Last 90 days', value: 90 },
]

/** Formats an API DateOnly string "yyyy-MM-dd" to a short "MMM d" label. */
export function shortDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  return Number.isNaN(d.getTime())
    ? iso
    : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

/** Human-friendly duration from a number of hours (or em dash when unknown). */
export function formatHours(hours: number | null | undefined): string {
  if (hours == null) return '—'
  if (hours < 1) return `${Math.round(hours * 60)}m`
  if (hours < 48) return `${hours.toFixed(1)}h`
  return `${Math.round(hours / 24)}d`
}

const AXIS_COLOR = '#94a3b8'
const GRID_COLOR = '#e5e7eb'

/** Gradient area chart options for a daily series. */
export function areaOptions(categories: string[], color: string): ApexOptions {
  return {
    chart: {
      type: 'area',
      toolbar: { show: false },
      zoom: { enabled: false },
      fontFamily: 'inherit',
      animations: { speed: 400 },
    },
    colors: [color],
    dataLabels: { enabled: false },
    stroke: { curve: 'smooth', width: 2 },
    fill: {
      type: 'gradient',
      gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0.05, stops: [0, 90, 100] },
    },
    xaxis: {
      categories,
      tickAmount: Math.min(7, categories.length),
      axisBorder: { show: false },
      axisTicks: { show: false },
      labels: { rotate: 0, hideOverlappingLabels: true, style: { colors: AXIS_COLOR } },
    },
    yaxis: {
      labels: { formatter: (v: number) => Math.round(v).toString(), style: { colors: AXIS_COLOR } },
    },
    legend: { show: false },
    grid: { borderColor: GRID_COLOR, strokeDashArray: 4 },
    tooltip: { shared: true },
  }
}

/** Horizontal bar chart options for a categorical breakdown. */
export function horizontalBarOptions(categories: string[], color: string): ApexOptions {
  return {
    chart: { type: 'bar', toolbar: { show: false }, fontFamily: 'inherit' },
    colors: [color],
    plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '65%', distributed: false } },
    dataLabels: { enabled: false },
    xaxis: {
      categories,
      axisBorder: { show: false },
      axisTicks: { show: false },
      labels: { style: { colors: AXIS_COLOR } },
    },
    yaxis: { labels: { style: { colors: '#64748b' } } },
    grid: { borderColor: GRID_COLOR, strokeDashArray: 4 },
    tooltip: { shared: false },
  }
}
