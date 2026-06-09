export function buildApiUrl(path: string) {
  const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').trim()
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  if (!apiBaseUrl) {
    if (typeof window !== 'undefined' && window.location.hostname) {
      return `http://${window.location.hostname}:8081${normalizedPath}`
    }
    return normalizedPath
  }
  return `${apiBaseUrl}${normalizedPath}`
}

export function buildChannelApiUrl(path: string, channelId: string) {
  const id = channelId.trim()
  const query = id ? `?channelId=${encodeURIComponent(id)}` : ''
  return `${buildApiUrl(path)}${query}`
}
