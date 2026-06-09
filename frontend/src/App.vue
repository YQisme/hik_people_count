<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import abnormalFallPromptAudio from './assets/audio/abnormal-fall.wav'
import abnormalFightPromptAudio from './assets/audio/abnormal-fight.wav'
import abnormalGatherPromptAudio from './assets/audio/abnormal-gather.wav'
import abnormalLeavePostPromptAudio from './assets/audio/abnormal-leavepost.wav'
import abnormalMessagePromptAudio from './assets/audio/abnormal-message.wav'
import abnormalPhonePromptAudio from './assets/audio/abnormal-phone.wav'
import abnormalPlayPhonePromptAudio from './assets/audio/abnormal-playphone.wav'
import abnormalPlayPhoneSpecificPromptAudio from './assets/audio/abnormal-playphone-specific.wav'
import abnormalSleepPromptAudio from './assets/audio/abnormal-sleep.wav'
import abnormalSmokePromptAudio from './assets/audio/abnormal-smoke.wav'
import abnormalTailgatingPromptAudio from './assets/audio/abnormal-tailgating.wav'
import alarmPromptAudio from './assets/audio/alarm.wav'
import areaAlertPromptAudio from './assets/audio/area-alert.wav'
import capacityExceededPromptAudio from './assets/audio/capacity-exceeded.wav'
import capacityFullPromptAudio from './assets/audio/capacity-full.wav'
import capacityNearLimitPromptAudio from './assets/audio/capacity-near-limit.wav'

declare global {
  interface Window {
    webkitAudioContext?: typeof AudioContext
  }
}

type AccessRecord = {
  id: string
  name: string
  department: string
  role: string
  enterTime: string
  gate: string
  avatarText: string
  card: string
  location: string
  phone: string
  team: string
  status: string
  direction: string
  stayDuration?: string
  imageUrl?: string
}

type MetricItem = {
  label: string
  value: number
  unit: string
  accent: string
}

type AlarmItem = {
  id: string
  code: string
  category: string
  level: string
  title: string
  detail: string
  status: string
  targetId: string
  targetName?: string
  gate?: string
  deviceIP?: string
  triggeredAt?: string
}

type AreaAlertState = {
  isActive: boolean
  hasPeople: number
  zoneName: string
  alertId: string
  triggeredAt: string
  updatedAt: string
  sourceTopic: string
  rawPayload: string
}

type AbnormalMessageItem = {
  id: string
  type: string
  time: string
  topic: string
  zoneName?: string
  conditionMet?: number | null
  receivedAt: string
  updatedAt: string
  rawPayload: string
  isHandled: boolean
  handledAt: string
  status: string
}

type DashboardResponse = {
  generatedAt?: string
  metrics?: MetricItem[]
  alarms?: AlarmItem[]
  recentRecords?: AccessRecord[]
  stayPeople?: AccessRecord[]
  selectedRecordId?: string
  areaAlert?: Partial<AreaAlertState> | null
  abnormalMessages?: Partial<AbnormalMessageItem>[]
}

type DeviceOption = {
  ip: string
  name: string
  deviceName: string
}

type HistoryEventItem = {
  time: string
  timeUtc?: string
  deviceIP: string
  deviceName: string
  majorType: string
  minorType: string
  cardNo: string
  employeeNo: string
  personName: string
  doorNo: number
  direction?: string
  imageUrl?: string
}

type HistoryEventsResponse = {
  events: HistoryEventItem[]
  totalMatches: number
  hasMore: boolean
  message?: string
}

type JsonRecord = Record<string, unknown>

const systemTitle = '限员管控系统'
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').trim()
const clock = ref(new Date())
const metrics = ref<MetricItem[]>([
  { label: '进场人数', value: 0, unit: '人', accent: 'cyan' },
  { label: '出场人数', value: 0, unit: '人', accent: 'teal' },
  { label: '限制人数', value: 500, unit: '人', accent: 'amber' },
  { label: '区域内停留人员', value: 0, unit: '人', accent: 'lime' },
])
const alarms = ref<AlarmItem[]>([])
const abnormalMessages = ref<AbnormalMessageItem[]>([])
const areaAlert = ref<AreaAlertState>({
  isActive: false,
  hasPeople: 0,
  zoneName: '',
  alertId: '',
  triggeredAt: '',
  updatedAt: '',
  sourceTopic: '',
  rawPayload: '',
})
const enterRecords = ref<AccessRecord[]>([])
const stayPeople = ref<AccessRecord[]>([])
const selectedRecordId = ref('')
const lastUpdatedAt = ref('--')
const loadError = ref('')
const audioEnabled = ref(false)
const audioMode = ref<'speech' | 'prompt' | 'tone' | 'none'>('none')
const audioHint = ref('轻触页面后自动启用语音播报')
const limitDraft = ref('500')
const isEditingLimit = ref(false)
const manualLimit = ref<number | null>(null)
const limitHint = ref('当前使用接口限制人数')
const stayWarningMinutesDraft = ref('30')
const isEditingStayWarningMinutes = ref(false)
const stayWarningMinutes = ref<number | null>(null)
const stayWarningHint = ref('当前使用服务端停留报警时间')
let clockTimer: number | undefined
let refreshTimer: number | undefined
let audioContext: AudioContext | null = null
let speechSynthesisRef: SpeechSynthesis | null = null
let promptAudioUnlocked = false
let knownAlarmIds = new Set<string>()
let knownAbnormalIds = new Set<string>()
let unlockAudioHandler: (() => void) | null = null
let areaAlertBroadcastTimer: number | undefined
const handledAreaAlertIds = ref<string[]>([])
const handledAbnormalIds = ref<string[]>([])
const historyVisible = ref(false)
const historyLoading = ref(false)
const historyError = ref('')
const historyDevices = ref<DeviceOption[]>([])
const historyDeviceIP = ref('')
const historyStartDate = ref('')
const historyStartClock = ref('')
const historyEndDate = ref('')
const historyEndClock = ref('')
const historyEvents = ref<AccessRecord[]>([])
const historyTotalMatches = ref(0)
const historyPage = ref(1)
const historyListRef = ref<HTMLElement | null>(null)
const HISTORY_PAGE_SIZE = 20

type AudioPromptKey =
  | 'alarm'
  | 'capacityExceeded'
  | 'capacityFull'
  | 'capacityNearLimit'
  | 'areaAlert'
  | 'abnormal'
  | 'tailgating'
  | 'fall'
  | 'phone'
  | 'smoke'
  | 'sleep'
  | 'fight'
  | 'playPhone'
  | 'leavePost'
  | 'gather'

const promptAudioSources: Record<AudioPromptKey, string[]> = {
  alarm: [alarmPromptAudio],
  capacityExceeded: [capacityExceededPromptAudio, alarmPromptAudio],
  capacityFull: [capacityFullPromptAudio, alarmPromptAudio],
  capacityNearLimit: [capacityNearLimitPromptAudio, alarmPromptAudio],
  areaAlert: [areaAlertPromptAudio],
  abnormal: [abnormalMessagePromptAudio],
  tailgating: [abnormalTailgatingPromptAudio, abnormalMessagePromptAudio],
  fall: [abnormalFallPromptAudio, abnormalMessagePromptAudio],
  phone: [abnormalPhonePromptAudio, abnormalMessagePromptAudio],
  smoke: [abnormalSmokePromptAudio, abnormalMessagePromptAudio],
  sleep: [abnormalSleepPromptAudio, abnormalMessagePromptAudio],
  fight: [abnormalFightPromptAudio, abnormalMessagePromptAudio],
  playPhone: [abnormalPlayPhoneSpecificPromptAudio, abnormalPlayPhonePromptAudio, abnormalMessagePromptAudio],
  leavePost: [abnormalLeavePostPromptAudio, abnormalMessagePromptAudio],
  gather: [abnormalGatherPromptAudio, abnormalMessagePromptAudio],
}

const promptAudioPlayers: Partial<Record<AudioPromptKey, HTMLAudioElement>> = {}

const emptyRecord: AccessRecord = {
  id: '暂无记录',
  name: '暂无人员信息',
  department: '未分配部门',
  role: '未设置岗位',
  enterTime: '--',
  gate: '--',
  avatarText: '暂无',
  card: '--',
  location: '--',
  phone: '--',
  team: '--',
  status: '等待门禁事件',
  direction: '--',
  stayDuration: '--',
}

function buildApiUrl(path: string) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  if (!apiBaseUrl) {
    if (typeof window !== 'undefined' && window.location.hostname) {
      return `http://${window.location.hostname}:8081${normalizedPath}`
    }
    return normalizedPath
  }
  return `${apiBaseUrl}${normalizedPath}`
}

function resolveImageUrl(url?: string) {
  const raw = url?.trim()
  if (!raw) {
    return ''
  }

  if (/^https?:\/\//i.test(raw)) {
    return raw
  }

  return buildApiUrl(raw)
}

function getMetricValue(label: string, fallback: number) {
  const metric = metrics.value.find((item) => item.label === label)
  const value = Number(metric?.value)
  return Number.isFinite(value) ? value : fallback
}

function formatLimitHint(limit: number | null) {
  return limit === null ? '当前使用服务端限制人数' : `已同步限制人数 ${limit} 人`
}

function formatStayWarningHint(minutes: number | null) {
  return minutes === null ? '当前使用服务端停留报警时间' : `已同步停留报警时间 ${minutes} 分钟`
}

function loadSavedLimit() {
  manualLimit.value = null
  limitHint.value = formatLimitHint(null)
}

function syncStayWarningDraft(minutes: number | null) {
  if (isEditingStayWarningMinutes.value || minutes === null) {
    return
  }

  stayWarningMinutesDraft.value = `${minutes}`
}

function syncLimitDraftFromMetrics() {
  if (isEditingLimit.value) {
    return
  }

  limitDraft.value = `${getMetricValue('限制人数', 500)}`
}

function parseStayWarningMinutesFromXml(xml: string) {
  const matchedMinutes = xml.match(/<StayWarningMinutes>\s*(\d+)\s*<\/StayWarningMinutes>/i)
  if (!matchedMinutes) {
    return null
  }

  const parsedMinutes = Number(matchedMinutes[1])
  return Number.isFinite(parsedMinutes) ? parsedMinutes : null
}

function updateStayWarningMinutesInXml(xml: string, nextMinutes: number) {
  if (/<StayWarningMinutes>[\s\S]*?<\/StayWarningMinutes>/i.test(xml)) {
    return xml.replace(/<StayWarningMinutes>[\s\S]*?<\/StayWarningMinutes>/i, `<StayWarningMinutes>${nextMinutes}</StayWarningMinutes>`)
  }

  if (/<\/Config>/i.test(xml)) {
    return xml.replace(/<\/Config>/i, `  <StayWarningMinutes>${nextMinutes}</StayWarningMinutes>\r\n  </Config>`)
  }

  throw new Error('配置中缺少 Config 节点')
}

async function loadStayWarningMinutes(options?: { silent?: boolean }) {
  try {
    const response = await fetch(buildApiUrl('/config'), {
      headers: {
        Accept: 'application/xml,text/xml,text/plain;q=0.9,*/*;q=0.8',
      },
    })

    if (!response.ok) {
      throw new Error(`停留报警时间读取失败(${response.status})`)
    }

    const xmlText = await response.text()
    const parsedMinutes = parseStayWarningMinutesFromXml(xmlText)
    if (parsedMinutes === null) {
      throw new Error('未读取到停留报警时间配置')
    }

    stayWarningMinutes.value = parsedMinutes
    syncStayWarningDraft(parsedMinutes)
    if (!options?.silent) {
      stayWarningHint.value = formatStayWarningHint(parsedMinutes)
    }
  } catch (error) {
    if (!options?.silent) {
      stayWarningHint.value = error instanceof Error ? error.message : '停留报警时间读取失败'
    }
  }
}

async function applyManualLimit() {
  const nextLimit = Number(limitDraft.value)
  if (!Number.isFinite(nextLimit) || nextLimit <= 0) {
    limitHint.value = '限制人数需填写大于 0 的整数'
    return
  }

  const normalizedLimit = Math.floor(nextLimit)
  try {
    const response = await fetch(buildApiUrl('/api/limit-count'), {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        limitCount: normalizedLimit,
      }),
    })

    if (!response.ok) {
      throw new Error(`限制人数保存失败(${response.status})`)
    }

    manualLimit.value = null
    isEditingLimit.value = false
    limitDraft.value = `${normalizedLimit}`
    limitHint.value = formatLimitHint(normalizedLimit)
    await loadDashboard()
  } catch (error) {
    limitHint.value = error instanceof Error ? error.message : '限制人数保存失败'
  }
}

async function applyStayWarningMinutes() {
  const nextMinutes = Number(stayWarningMinutesDraft.value)
  if (!Number.isFinite(nextMinutes) || nextMinutes <= 0) {
    stayWarningHint.value = '停留报警时间需填写大于 0 的整数'
    return
  }

  const normalizedMinutes = Math.floor(nextMinutes)
  try {
    const configResponse = await fetch(buildApiUrl('/config'), {
      headers: {
        Accept: 'application/xml,text/xml,text/plain;q=0.9,*/*;q=0.8',
      },
    })

    if (!configResponse.ok) {
      throw new Error(`停留报警时间读取失败(${configResponse.status})`)
    }

    const currentXml = await configResponse.text()
    const nextXml = updateStayWarningMinutesInXml(currentXml, normalizedMinutes)
    const body = new URLSearchParams()
    body.set('xml', nextXml)

    const saveResponse = await fetch(buildApiUrl('/config'), {
      method: 'POST',
      headers: {
        Accept: 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
        'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
      },
      body: body.toString(),
    })

    if (!saveResponse.ok) {
      throw new Error(`停留报警时间保存失败(${saveResponse.status})`)
    }

    stayWarningMinutes.value = normalizedMinutes
    isEditingStayWarningMinutes.value = false
    stayWarningMinutesDraft.value = `${normalizedMinutes}`
    stayWarningHint.value = `已保存停留报警时间 ${normalizedMinutes} 分钟`
    await loadStayWarningMinutes({ silent: true })
  } catch (error) {
    stayWarningHint.value = error instanceof Error ? error.message : '停留报警时间保存失败'
  }
}

function normalizeDirection(value?: string) {
  const raw = value?.trim() || ''
  const normalized = raw.toLowerCase()
  if (normalized === 'in' || normalized === 'enter' || normalized === '进入' || normalized === '进') {
    return '进'
  }
  if (normalized === 'out' || normalized === 'exit' || normalized === '出去' || normalized === '出') {
    return '出'
  }
  return raw || '--'
}

function padDatePart(value: number) {
  return `${value}`.padStart(2, '0')
}

function formatLocalDate(date: Date) {
  return `${date.getFullYear()}-${padDatePart(date.getMonth() + 1)}-${padDatePart(date.getDate())}`
}

function formatLocalClock(date: Date) {
  return `${padDatePart(date.getHours())}:${padDatePart(date.getMinutes())}`
}

function setHistoryRange(start: Date, end: Date) {
  historyStartDate.value = formatLocalDate(start)
  historyStartClock.value = formatLocalClock(start)
  historyEndDate.value = formatLocalDate(end)
  historyEndClock.value = formatLocalClock(end)
}

function buildHistoryDateTime(dateValue: string, clockValue: string) {
  const date = dateValue.trim()
  const clock = clockValue.trim()
  if (!date || !clock) {
    return ''
  }

  const normalizedClock = /^\d{2}:\d{2}:\d{2}$/.test(clock) ? clock : `${clock}:00`
  return `${date}T${normalizedClock}`
}

function initHistoryTimeRange() {
  const now = new Date()
  const start = new Date(now)
  start.setHours(0, 0, 0, 0)
  setHistoryRange(start, now)
}

function applyHistoryPreset(preset: 'today' | 'yesterday' | 'last7' | 'last30') {
  const now = new Date()
  const end = new Date(now)
  const start = new Date(now)

  switch (preset) {
    case 'today':
      start.setHours(0, 0, 0, 0)
      break
    case 'yesterday':
      start.setDate(start.getDate() - 1)
      start.setHours(0, 0, 0, 0)
      end.setDate(end.getDate() - 1)
      end.setHours(23, 59, 59, 0)
      break
    case 'last7':
      start.setDate(start.getDate() - 6)
      start.setHours(0, 0, 0, 0)
      break
    case 'last30':
      start.setDate(start.getDate() - 29)
      start.setHours(0, 0, 0, 0)
      break
  }

  setHistoryRange(start, end)
}

function normalizeHistoryEvent(event: Partial<HistoryEventItem>): AccessRecord {
  const name = event.personName?.trim() || '未知人员'
  const gateLabel =
    event.direction && event.direction !== '未知'
      ? `${event.direction} / 门${event.doorNo || 1}`
      : `门${event.doorNo || 1}`

  return normalizeRecord({
    id: event.employeeNo?.trim() || event.cardNo?.trim() || '未知工号',
    name,
    department: event.deviceName?.trim() || event.deviceIP?.trim() || '未知设备',
    role: event.minorType?.trim() || event.majorType?.trim() || '历史事件',
    enterTime: event.time?.trim() || '--',
    gate: gateLabel,
    direction: event.direction,
    card: event.cardNo?.trim() || event.minorType?.trim() || '--',
    status: event.majorType?.trim() || '历史记录',
    imageUrl: event.imageUrl?.trim(),
  })
}

async function loadHistoryDevices() {
  const response = await fetch(buildApiUrl('/api/devices'), {
    headers: { Accept: 'application/json' },
  })
  if (!response.ok) {
    throw new Error(`设备列表接口返回 ${response.status}`)
  }

  const payload = (await response.json()) as DeviceOption[]
  historyDevices.value = Array.isArray(payload) ? payload : []
  if (!historyDeviceIP.value && historyDevices.value.length) {
    historyDeviceIP.value = historyDevices.value[0].ip
  }
}

async function openHistoryPanel() {
  historyVisible.value = true
  historyError.value = ''
  initHistoryTimeRange()

  try {
    await loadHistoryDevices()
  } catch (error) {
    historyError.value = error instanceof Error ? error.message : '加载设备列表失败'
  }
}

function closeHistoryPanel() {
  historyVisible.value = false
  historyLoading.value = false
  historyError.value = ''
}

async function fetchHistoryPage(page: number) {
  const startTime = buildHistoryDateTime(historyStartDate.value, historyStartClock.value)
  const endTime = buildHistoryDateTime(historyEndDate.value, historyEndClock.value)
  if (!startTime || !endTime) {
    throw new Error('请在日历中选择开始和结束时间')
  }

  const response = await fetch(buildApiUrl('/api/acs-events/history'), {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      deviceIP: historyDeviceIP.value,
      startTime,
      endTime,
      major: 5,
      minor: 0,
      maxResults: HISTORY_PAGE_SIZE,
      searchResultPosition: (page - 1) * HISTORY_PAGE_SIZE,
      fetchAll: false,
    }),
  })

  const payload = (await response.json()) as HistoryEventsResponse
  if (!response.ok) {
    throw new Error(payload.message || `历史事件接口返回 ${response.status}`)
  }

  historyEvents.value = sortRecordsNewestFirst((payload.events ?? []).map(normalizeHistoryEvent))
  historyTotalMatches.value = Number(payload.totalMatches) || historyEvents.value.length
  historyPage.value = page

  if (historyListRef.value) {
    historyListRef.value.scrollTop = 0
  }
}

async function searchHistoryEvents() {
  if (!historyDeviceIP.value) {
    historyError.value = '请选择门禁设备'
    return
  }

  historyLoading.value = true
  historyError.value = ''
  historyEvents.value = []
  historyTotalMatches.value = 0
  historyPage.value = 1

  try {
    await fetchHistoryPage(1)
  } catch (error) {
    historyEvents.value = []
    historyTotalMatches.value = 0
    historyPage.value = 1
    historyError.value = error instanceof Error ? error.message : '查询历史事件失败'
  } finally {
    historyLoading.value = false
  }
}

async function goToHistoryPage(page: number) {
  if (historyLoading.value || page < 1 || page > historyTotalPages.value || page === historyPage.value) {
    return
  }

  historyLoading.value = true
  historyError.value = ''

  try {
    await fetchHistoryPage(page)
  } catch (error) {
    historyError.value = error instanceof Error ? error.message : '切换分页失败'
  } finally {
    historyLoading.value = false
  }
}

function normalizeRecord(record: Partial<AccessRecord>): AccessRecord {
  const name = record.name?.trim() || '未知员工'
  const avatarText = record.avatarText?.trim() || name.replace(/[()（）\s]/g, '').slice(0, 2) || '未知'
  return {
    id: record.id?.trim() || '未知工号',
    name,
    department: record.department?.trim() || '未分配部门',
    role: record.role?.trim() || '未设置岗位',
    enterTime: record.enterTime?.trim() || '--',
    gate: record.gate?.trim() || '--',
    avatarText,
    card: record.card?.trim() || '--',
    location: record.location?.trim() || '--',
    phone: record.phone?.trim() || '--',
    team: record.team?.trim() || '--',
    status: record.status?.trim() || '识别通过',
    direction: normalizeDirection(record.direction),
    stayDuration: record.stayDuration?.trim(),
    imageUrl: resolveImageUrl(record.imageUrl),
  }
}

function parseRecordTime(value?: string) {
  const raw = value?.trim()
  if (!raw || raw === '--') {
    return Number.NEGATIVE_INFINITY
  }

  const normalized = raw.replace(/\.\d+$/, '').replace(' ', 'T')
  const timestamp = Date.parse(normalized)
  if (Number.isFinite(timestamp)) {
    return timestamp
  }

  return raw.replace(/\D/g, '').padEnd(14, '0')
}

function sortRecordsNewestFirst(records: AccessRecord[]) {
  return [...records]
    .map((record, index) => ({
      record,
      index,
      sortKey: parseRecordTime(record.enterTime),
    }))
    .sort((left, right) => {
      if (left.sortKey === right.sortKey) {
        return left.index - right.index
      }

      if (typeof left.sortKey === 'number' && typeof right.sortKey === 'number') {
        return right.sortKey - left.sortKey
      }

      return `${right.sortKey}`.localeCompare(`${left.sortKey}`)
    })
    .map((item) => item.record)
}

function normalizeAlarm(item: Partial<AlarmItem>): AlarmItem {
  const code = decodeEscapedText(item.code).trim() || 'UNKNOWN_ALARM'
  const category = decodeEscapedText(item.category).trim() || 'general'
  const title = decodeEscapedText(item.title).trim() || '暂无报警'
  const rawDetail = decodeEscapedText(item.detail).trim() || '当前没有报警信息'
  const detail = isAbnormalStayAlarm({ code, category, title, detail: rawDetail })
    ? stripAlarmCurrentLocation(rawDetail) || '检测到异常滞留，请及时处理。'
    : rawDetail
  const id = item.id?.trim() || `${code || title || 'alarm'}-${item.targetId || 'global'}`
  return {
    id,
    code,
    category,
    level: decodeEscapedText(item.level).trim() || '普通提醒',
    title,
    detail,
    status: decodeEscapedText(item.status).trim() || '待处理',
    targetId: decodeEscapedText(item.targetId).trim() || '',
    targetName: decodeEscapedText(item.targetName).trim() || '',
    gate: decodeEscapedText(item.gate).trim() || '',
    deviceIP: decodeEscapedText(item.deviceIP).trim() || '',
    triggeredAt: decodeEscapedText(item.triggeredAt).trim() || '',
  }
}

function normalizeAreaAlert(state?: Partial<AreaAlertState> | null): AreaAlertState {
  return {
    isActive: !!state?.isActive,
    hasPeople: Number(state?.hasPeople ?? 0),
    zoneName: state?.zoneName?.trim() || '',
    alertId: state?.alertId?.trim() || '',
    triggeredAt: state?.triggeredAt?.trim() || '',
    updatedAt: state?.updatedAt?.trim() || '',
    sourceTopic: state?.sourceTopic?.trim() || '',
    rawPayload: state?.rawPayload?.trim() || '',
  }
}

function decodeEscapedText(value?: string | null) {
  if (!value) {
    return ''
  }

  let decoded = value
  for (let index = 0; index < 3; index += 1) {
    const next = decoded
      .replace(/\\u([0-9a-fA-F]{4})/g, (_, hex: string) => String.fromCharCode(Number.parseInt(hex, 16)))
      .replace(/\\r/g, '\r')
      .replace(/\\n/g, '\n')
      .replace(/\\t/g, '\t')

    if (next === decoded) {
      break
    }
    decoded = next
  }

  return decoded
}

function stripAlarmCurrentLocation(detail?: string | null) {
  const normalizedDetail = decodeEscapedText(detail).trim()
  if (!normalizedDetail) {
    return ''
  }

  return normalizedDetail
    .replace(/(?:[，,；;。]\s*|\s+)?当前位置(?:[:：]?\s*|为\s*).+$/u, '')
    .replace(/[，,；;。]\s*$/u, '')
    .trim()
}

function isAbnormalStayAlarm(alarm: Partial<AlarmItem>) {
  const code = decodeEscapedText(alarm.code).trim().toLowerCase()
  const category = decodeEscapedText(alarm.category).trim().toLowerCase()
  const title = decodeEscapedText(alarm.title).trim()
  const detail = decodeEscapedText(alarm.detail).trim()

  return (
    title.includes('异常滞留') ||
    detail.includes('异常滞留') ||
    code.includes('stay') ||
    code.includes('linger') ||
    code.includes('loiter') ||
    category.includes('stay')
  )
}

function decodePayloadValue(value: unknown): unknown {
  if (typeof value === 'string') {
    return decodeEscapedText(value)
  }

  if (Array.isArray(value)) {
    return value.map((item) => decodePayloadValue(item))
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, decodePayloadValue(item)]),
    )
  }

  return value
}

function parseAbnormalPayload(rawPayload?: string | null) {
  const decodedPayload = decodeEscapedText(rawPayload).trim()
  if (!decodedPayload) {
    return {
      formattedPayload: '',
      parsedPayload: null as JsonRecord | null,
    }
  }

  try {
    const parsed = decodePayloadValue(JSON.parse(decodedPayload))
    const formattedPayload = JSON.stringify(parsed, null, 2)
    return {
      formattedPayload,
      parsedPayload:
        parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as JsonRecord) : null,
    }
  } catch {
    return {
      formattedPayload: decodedPayload,
      parsedPayload: null as JsonRecord | null,
    }
  }
}

function getPayloadTextValue(payload: JsonRecord | null, key: string) {
  const value = payload?.[key]
  return typeof value === 'string' ? value.trim() : ''
}

function getPayloadNumberValue(payload: JsonRecord | null, key: string) {
  const value = payload?.[key]
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }
  if (typeof value === 'string') {
    const parsed = Number(value.trim())
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function formatAbnormalMessageTitle(message: AbnormalMessageItem) {
  const zoneName = decodeEscapedText(message.zoneName).trim()
  const abnormalType = normalizeAbnormalTypeLabel(message)

  if (isTailgatingAbnormalMessage(message)) {
    return zoneName ? `${zoneName}人员靠近提醒` : '人员靠近提醒'
  }

  if (!zoneName || zoneName === abnormalType) {
    return `发现${abnormalType}异常`
  }

  return zoneName ? `${zoneName}发现${abnormalType}异常` : `发现${abnormalType}异常`
}

function normalizeAbnormalMessage(item?: Partial<AbnormalMessageItem> | null): AbnormalMessageItem {
  const { formattedPayload, parsedPayload } = parseAbnormalPayload(item?.rawPayload)
  const type = decodeEscapedText(item?.type).trim() || getPayloadTextValue(parsedPayload, 'type') || 'abnormal'
  const time =
    decodeEscapedText(item?.time).trim() ||
    decodeEscapedText(item?.receivedAt).trim() ||
    getPayloadTextValue(parsedPayload, 'time') ||
    '--'
  const topic = decodeEscapedText(item?.topic).trim() || 'abnormal'
  const zoneName =
    decodeEscapedText(item?.zoneName).trim() ||
    getPayloadTextValue(parsedPayload, 'zoneName') ||
    getPayloadTextValue(parsedPayload, 'zone')
  const conditionMet =
    typeof item?.conditionMet === 'number' ? item.conditionMet : getPayloadNumberValue(parsedPayload, 'conditionMet')
  return {
    id: item?.id?.trim() || `${topic}-${type}-${time}`,
    type,
    time,
    topic,
    zoneName,
    conditionMet,
    receivedAt: decodeEscapedText(item?.receivedAt).trim() || time,
    updatedAt: decodeEscapedText(item?.updatedAt).trim() || decodeEscapedText(item?.receivedAt).trim() || time,
    rawPayload: formattedPayload,
    isHandled: !!item?.isHandled,
    handledAt: item?.handledAt?.trim() || '',
    status: item?.status?.trim() || (item?.isHandled ? '已处理' : '待处理'),
  }
}

function getAudioCtor() {
  if (typeof window === 'undefined') {
    return null
  }
  return window.AudioContext ?? window.webkitAudioContext ?? null
}

function getSpeechSynthesis() {
  if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
    return null
  }
  return window.speechSynthesis
}

function canUsePromptAudio() {
  return typeof Audio !== 'undefined'
}

function isTouchAudioDevice() {
  if (typeof window === 'undefined' || typeof navigator === 'undefined') {
    return false
  }

  return navigator.maxTouchPoints > 0 || 'ontouchstart' in window
}

function getPromptAudioElement(key: AudioPromptKey) {
  if (typeof window === 'undefined' || !canUsePromptAudio()) {
    return null
  }

  if (!promptAudioPlayers[key]) {
    const audio = new Audio(promptAudioSources[key][0])
    audio.preload = 'auto'
    audio.setAttribute('playsinline', 'true')
    audio.setAttribute('webkit-playsinline', 'true')
    promptAudioPlayers[key] = audio
  }

  return promptAudioPlayers[key] ?? null
}

async function unlockPromptAudio() {
  if (!canUsePromptAudio()) {
    return false
  }

  let unlocked = false
  const keys = Object.keys(promptAudioSources) as AudioPromptKey[]
  for (const key of keys) {
    const audio = getPromptAudioElement(key)
    if (!audio) {
      continue
    }

    try {
      audio.muted = true
      audio.currentTime = 0
      await audio.play()
      audio.pause()
      audio.currentTime = 0
      audio.muted = false
      unlocked = true
    } catch {
      audio.muted = false
    }
  }

  promptAudioUnlocked = unlocked || promptAudioUnlocked
  return promptAudioUnlocked
}

function getPreferredSpeechVoice(synth: SpeechSynthesis) {
  const voices = synth.getVoices()
  return (
    voices.find((voice) => /zh(-|_)?cn/i.test(voice.lang)) ??
    voices.find((voice) => voice.lang.toLowerCase().startsWith('zh')) ??
    voices.find((voice) => /mandarin|chinese|中文/i.test(`${voice.name} ${voice.lang}`)) ??
    null
  )
}

function speakText(text: string, options?: { interrupt?: boolean }) {
  const synth = speechSynthesisRef ?? getSpeechSynthesis()
  const normalizedText = text.replace(/\s+/g, ' ').trim()

  if (!synth || typeof SpeechSynthesisUtterance === 'undefined' || !normalizedText) {
    return false
  }

  speechSynthesisRef = synth

  if (options?.interrupt ?? true) {
    synth.cancel()
  } else if (synth.speaking || synth.pending) {
    return true
  }

  const utterance = new SpeechSynthesisUtterance(normalizedText)
  const voice = getPreferredSpeechVoice(synth)

  utterance.lang = voice?.lang || 'zh-CN'
  utterance.rate = 1
  utterance.pitch = 1
  utterance.volume = 1

  if (voice) {
    utterance.voice = voice
  }

  try {
    synth.speak(utterance)
    return true
  } catch {
    return false
  }
}

function stopPromptAudioPlayback() {
  const keys = Object.keys(promptAudioPlayers) as AudioPromptKey[]
  keys.forEach((key) => {
    const audio = promptAudioPlayers[key]
    if (!audio) {
      return
    }

    try {
      audio.pause()
      audio.currentTime = 0
    } catch {
      // ignore media stop errors on constrained browsers
    }
  })
}

function playPromptAudio(key: AudioPromptKey, options?: { interrupt?: boolean }) {
  const audio = getPromptAudioElement(key)
  if (!audio) {
    return false
  }

  if (options?.interrupt ?? true) {
    stopPromptAudioPlayback()
  }

  try {
    audio.pause()
    audio.currentTime = 0
    const candidates = promptAudioSources[key]
    const tryPlay = (candidateIndex: number) => {
      if (candidateIndex >= candidates.length) {
        playAlarmTone()
        return
      }

      const nextSrc = candidates[candidateIndex]
      if (!audio.src || !audio.src.includes(nextSrc.replace(/^\//, ''))) {
        audio.src = nextSrc
        audio.load()
      }

      const playTask = audio.play()
      if (playTask && typeof playTask.catch === 'function') {
        void playTask.catch(() => {
          tryPlay(candidateIndex + 1)
        })
      }
    }

    tryPlay(0)
    return true
  } catch {
    return false
  }
}

async function enableAlarmSound() {
  speechSynthesisRef = getSpeechSynthesis()
  const AudioCtor = getAudioCtor()
  const canSpeak = !!speechSynthesisRef && typeof SpeechSynthesisUtterance !== 'undefined'
  const canPlayTone = !!AudioCtor
  const canPlayPrompt = canUsePromptAudio()
  const preferPrompt = isTouchAudioDevice() && canPlayPrompt

  if (!canSpeak && !canPlayTone && !canPlayPrompt) {
    audioMode.value = 'none'
    audioHint.value = '当前浏览器不支持报警播报'
    audioEnabled.value = false
    return
  }

  if (AudioCtor && !audioContext) {
    audioContext = new AudioCtor()
  }

  if (audioContext?.state === 'suspended') {
    await audioContext.resume()
  }

  if (speechSynthesisRef) {
    speechSynthesisRef.getVoices()
  }

  if (canPlayPrompt) {
    await unlockPromptAudio()
  }

  audioMode.value = preferPrompt ? 'prompt' : canSpeak ? 'speech' : canPlayPrompt ? 'prompt' : 'tone'
  audioEnabled.value = true
  audioHint.value = preferPrompt
    ? '已启用语音播报'
    : canSpeak
      ? '语音播报已开启'
      : canPlayPrompt
        ? '浏览器语音不可用，已启用录音播报'
        : '语音播报不可用，已启用提示音'

  if (isAreaAlertPending(areaAlert.value)) {
    notifyAreaAlert(areaAlert.value)
    startAreaAlertBroadcastLoop()
    return
  }

  const latestAbnormalMessage = activeAbnormalMessages.value[0]
  if (latestAbnormalMessage && shouldBroadcastAbnormalMessage(latestAbnormalMessage)) {
    broadcastAlarm(buildAbnormalBroadcastText(latestAbnormalMessage), getAbnormalBroadcastOptions(latestAbnormalMessage))
    return
  }

  const latestAlarm = visibleAlarms.value[0]
  if (latestAlarm) {
    broadcastAlarm(buildAlarmBroadcastText(latestAlarm), getAlarmBroadcastOptions(latestAlarm))
  }
}

function playAlarmTone() {
  if (!audioEnabled.value || !audioContext) {
    return
  }

  const ctx = audioContext
  const startAt = ctx.currentTime + 0.02
  const tones = [880, 660, 880]

  tones.forEach((frequency, index) => {
    const oscillator = ctx.createOscillator()
    const gainNode = ctx.createGain()
    const offset = startAt + index * 0.24
    oscillator.type = 'square'
    oscillator.frequency.setValueAtTime(frequency, offset)
    gainNode.gain.setValueAtTime(0.0001, offset)
    gainNode.gain.exponentialRampToValueAtTime(0.16, offset + 0.02)
    gainNode.gain.exponentialRampToValueAtTime(0.0001, offset + 0.18)
    oscillator.connect(gainNode)
    gainNode.connect(ctx.destination)
    oscillator.start(offset)
    oscillator.stop(offset + 0.2)
  })
}

function broadcastAlarm(text: string, options?: { interrupt?: boolean; promptKey?: AudioPromptKey; preferSpeech?: boolean; forcePrompt?: boolean }) {
  if (!audioEnabled.value) {
    return
  }

  if (options?.interrupt ?? true) {
    speechSynthesisRef?.cancel()
  }

  const shouldTrySpeech = !options?.forcePrompt && (audioMode.value === 'speech' || options?.preferSpeech) && !!speechSynthesisRef
  const hasSpeech = shouldTrySpeech && speakText(text, options)
  const hasPrompt = !hasSpeech && !!options?.promptKey && playPromptAudio(options.promptKey, options)
  if (!hasSpeech && !hasPrompt) {
    playAlarmTone()
  }
}

function buildAreaAlertBroadcastText(alert: AreaAlertState) {
  const zoneName = alert.zoneName || '未知区域'
  const peopleCount = Number.isFinite(alert.hasPeople) ? Math.max(0, alert.hasPeople) : 0
  return `${zoneName}检测到${peopleCount}人，请立即处理区域报警。`
}

function extractAlarmTargetName(alarm: AlarmItem) {
  const targetName = decodeEscapedText(alarm.targetName).trim()
  if (targetName) {
    return targetName
  }

  const matchedStayPerson = stayPeople.value.find((person) => person.id === alarm.targetId)
  if (matchedStayPerson?.name) {
    return matchedStayPerson.name
  }

  const detail = decodeEscapedText(alarm.detail).trim()
  const matchedName = detail.match(/^(.+?)\s*已停留/u)
  return matchedName?.[1]?.trim() || ''
}

function extractAlarmStayDuration(alarm: AlarmItem) {
  const matchedStayPerson = stayPeople.value.find((person) => person.id === alarm.targetId || person.name === alarm.targetName)
  if (matchedStayPerson?.stayDuration?.trim()) {
    return matchedStayPerson.stayDuration.trim()
  }

  const detail = decodeEscapedText(alarm.detail).trim()
  const matchedDuration = detail.match(/已停留\s*([0-9]{1,2}:[0-9]{2}(?::[0-9]{2})?|\d+\s*小时\s*\d+\s*分钟|\d+\s*分钟|\d+\s*分(?:钟)?)/u)
  return matchedDuration?.[1]?.trim() || ''
}

function extractStayMinutesText(duration: string) {
  const normalizedDuration = decodeEscapedText(duration).trim()
  if (!normalizedDuration) {
    return ''
  }

  const colonParts = normalizedDuration.split(':')
  if (colonParts.length === 2 || colonParts.length === 3) {
    const numericParts = colonParts.map((part) => Number(part))
    if (numericParts.every((part) => Number.isFinite(part))) {
      const [hours, minutes] = colonParts.length === 3 ? numericParts : [0, numericParts[0]]
      const totalMinutes = Math.max(0, hours * 60 + minutes)
      return `${totalMinutes}分钟`
    }
  }

  const hourMinuteMatch = normalizedDuration.match(/(?:(\d+)\s*小时)?\s*(?:(\d+)\s*分(?:钟)?)/u)
  if (hourMinuteMatch) {
    const hours = Number(hourMinuteMatch[1] || 0)
    const minutes = Number(hourMinuteMatch[2] || 0)
    if (Number.isFinite(hours) && Number.isFinite(minutes)) {
      return `${hours * 60 + minutes}分钟`
    }
  }

  const minuteMatch = normalizedDuration.match(/(\d+)\s*分(?:钟)?/u)
  if (minuteMatch) {
    return `${minuteMatch[1]}分钟`
  }

  return normalizedDuration
}

function buildAbnormalStayAlarmBroadcastText(alarm: AlarmItem) {
  const personName = extractAlarmTargetName(alarm) || '有人'
  const stayDuration = extractStayMinutesText(extractAlarmStayDuration(alarm))
  if (stayDuration) {
    return `${personName}已停留${stayDuration}，请及时处理。`
  }
  return `${personName}出现异常滞留，请及时处理。`
}

function getCapacityAlarmPromptKey(alarm: AlarmItem) {
  const code = decodeEscapedText(alarm.code).trim().toUpperCase()
  const title = decodeEscapedText(alarm.title).trim()
  const detail = decodeEscapedText(alarm.detail).trim()
  const combinedText = `${title} ${detail}`

  if (code === 'CAPACITY_EXCEEDED' || combinedText.includes('超员')) {
    return 'capacityExceeded' as AudioPromptKey
  }
  if (code === 'CAPACITY_FULL' || combinedText.includes('满员') || combinedText.includes('只出不进')) {
    return 'capacityFull' as AudioPromptKey
  }
  if (code === 'CAPACITY_NEAR_LIMIT' || combinedText.includes('临界预警') || combinedText.includes('仅差 1 人')) {
    return 'capacityNearLimit' as AudioPromptKey
  }
  return null
}

function getAlarmBroadcastOptions(alarm: AlarmItem) {
  const capacityPromptKey = getCapacityAlarmPromptKey(alarm)
  if (capacityPromptKey) {
    return { promptKey: capacityPromptKey, forcePrompt: true }
  }

  if (isAbnormalStayAlarm(alarm)) {
    return { promptKey: 'alarm' as AudioPromptKey }
  }

  return { promptKey: 'alarm' as AudioPromptKey }
}

function buildAlarmBroadcastText(alarm: AlarmItem) {
  if (isAbnormalStayAlarm(alarm)) {
    return buildAbnormalStayAlarmBroadcastText(alarm)
  }
  const title = alarm.title || '发现新的报警'
  const detail = alarm.detail || `${alarm.targetName || '当前区域'}出现异常，请尽快处理。`
  return `${title}。${detail}`
}

function isTailgatingAbnormalMessage(message: AbnormalMessageItem) {
  const abnormalType = decodeEscapedText(message.type).trim()
  return abnormalType.includes('尾随闯入') || abnormalType.includes('尾随')
}

function normalizeAbnormalTypeKey(message: AbnormalMessageItem) {
  const abnormalType = decodeEscapedText(message.type)
    .trim()
    .toLowerCase()
    .replace(/[\s_-]+/g, '')

  if (abnormalType.includes('尾随')) {
    return 'abnormal'
  }

  if (abnormalType.includes('摔')) {
    return 'fall'
  }

  if (abnormalType.includes('手机')) {
    return abnormalType.includes('打') || abnormalType.includes('call') ? 'phone' : 'playPhone'
  }

  if (
    abnormalType === 'phone' ||
    abnormalType === 'callphone' ||
    abnormalType === 'callingphone' ||
    abnormalType === 'makephonecall' ||
    abnormalType === '打电话'
  ) {
    return 'phone'
  }
  if (abnormalType === 'fall' || abnormalType === '摔倒') {
    return 'fall'
  }
  if (abnormalType === 'smoke' || abnormalType === '抽烟') {
    return 'smoke'
  }
  if (abnormalType === 'sleep' || abnormalType === '睡岗') {
    return 'sleep'
  }
  if (abnormalType === 'fight' || abnormalType === '打架') {
    return 'fight'
  }
  if (
    abnormalType === 'playphone' ||
    abnormalType === 'usephone' ||
    abnormalType === 'usingphone' ||
    abnormalType === 'mobilephone' ||
    abnormalType === 'cellphone' ||
    abnormalType === 'smartphone' ||
    abnormalType === 'lookphone' ||
    abnormalType === 'watchphone' ||
    abnormalType === '玩手机' ||
    abnormalType === '看手机'
  ) {
    return 'playPhone'
  }
  if (abnormalType === 'leavepost' || abnormalType === '离岗') {
    return 'leavePost'
  }
  if (abnormalType === 'gather' || abnormalType === '聚众') {
    return 'gather'
  }

  return 'abnormal'
}

function normalizeAbnormalTypeLabel(message: AbnormalMessageItem) {
  const abnormalTypeKey = normalizeAbnormalTypeKey(message)
  if (abnormalTypeKey === 'phone') {
    return '打电话'
  }
  if (abnormalTypeKey === 'fall') {
    return '摔倒'
  }
  if (abnormalTypeKey === 'smoke') {
    return '抽烟'
  }
  if (abnormalTypeKey === 'sleep') {
    return '睡岗'
  }
  if (abnormalTypeKey === 'fight') {
    return '打架'
  }
  if (abnormalTypeKey === 'playPhone') {
    return '玩手机'
  }
  if (abnormalTypeKey === 'leavePost') {
    return '离岗'
  }
  if (abnormalTypeKey === 'gather') {
    return '聚众'
  }

  return decodeEscapedText(message.type).trim() || '异常行为'
}

function shouldBroadcastAbnormalMessage(message: AbnormalMessageItem) {
  if (isTailgatingAbnormalMessage(message)) {
    return message.conditionMet === 1
  }

  return message.conditionMet !== 0
}

function getAbnormalBroadcastOptions(message: AbnormalMessageItem) {
  if (isTailgatingAbnormalMessage(message)) {
    return { promptKey: 'tailgating' as AudioPromptKey, forcePrompt: true }
  }

  const abnormalTypeKey = normalizeAbnormalTypeKey(message)
  if (abnormalTypeKey !== 'abnormal') {
    return { promptKey: abnormalTypeKey as AudioPromptKey, forcePrompt: true }
  }

  return { promptKey: 'abnormal' as AudioPromptKey }
}

function buildAbnormalBroadcastText(message: AbnormalMessageItem) {
  if (isTailgatingAbnormalMessage(message)) {
    const zoneName = decodeEscapedText(message.zoneName).trim() || '当前通道'
    return `${zoneName}请保持安全距离，请在警戒线外有序排队，依次刷脸通行。`
  }

  const abnormalType = normalizeAbnormalTypeLabel(message)
  const zoneName = decodeEscapedText(message.zoneName).trim()
  return zoneName ? `${zoneName}发生${abnormalType}异常，请及时处理。` : `检测到${abnormalType}异常，请及时处理。`
}

function isAreaAlertPending(alert: AreaAlertState) {
  const currentId = alert.alertId || `${alert.zoneName}-${alert.updatedAt}`
  return alert.isActive && !handledAreaAlertIds.value.includes(currentId)
}

function notifyAreaAlert(alert: AreaAlertState, options?: { interrupt?: boolean }) {
  broadcastAlarm(buildAreaAlertBroadcastText(alert), { ...options, promptKey: 'areaAlert' })
}

function stopAreaAlertBroadcastLoop() {
  if (areaAlertBroadcastTimer) {
    window.clearInterval(areaAlertBroadcastTimer)
    areaAlertBroadcastTimer = undefined
  }
}

function startAreaAlertBroadcastLoop() {
  stopAreaAlertBroadcastLoop()
  if (!audioEnabled.value || !isAreaAlertPending(areaAlert.value)) {
    return
  }

  areaAlertBroadcastTimer = window.setInterval(() => {
    notifyAreaAlert(areaAlert.value, { interrupt: false })
  }, 10000)
}

function syncAreaAlertState(previousAlert: AreaAlertState, nextAlert: AreaAlertState) {
  const previousSignature = previousAlert.alertId || (previousAlert.isActive ? `${previousAlert.zoneName}-${previousAlert.updatedAt}` : '')
  const nextSignature = nextAlert.alertId || (nextAlert.isActive ? `${nextAlert.zoneName}-${nextAlert.updatedAt}` : '')
  const isNewActivation = nextAlert.isActive && nextSignature !== previousSignature
  const isHandled = !!nextSignature && handledAreaAlertIds.value.includes(nextSignature)

  areaAlert.value = nextAlert

  if (nextAlert.isActive && !isHandled) {
    if (isNewActivation) {
      notifyAreaAlert(nextAlert)
    }
    startAreaAlertBroadcastLoop()
    return
  }

  stopAreaAlertBroadcastLoop()
}

function handleAreaAlert() {
  const currentId = areaAlert.value.alertId || `${areaAlert.value.zoneName}-${areaAlert.value.updatedAt}`
  if (currentId && !handledAreaAlertIds.value.includes(currentId)) {
    handledAreaAlertIds.value = [...handledAreaAlertIds.value, currentId]
  }
  speechSynthesisRef?.cancel()
  stopAreaAlertBroadcastLoop()
  audioHint.value = '当前区域报警已处理'
}

function syncAbnormalState(nextMessages: AbnormalMessageItem[]) {
  const serverActiveIds = new Set(nextMessages.filter((message) => !message.isHandled).map((message) => message.id))
  if (handledAbnormalIds.value.length) {
    handledAbnormalIds.value = handledAbnormalIds.value.filter((id) => !serverActiveIds.has(id))
  }

  const activeMessages = [...nextMessages]
    .filter((message) => !message.isHandled)
    .sort((left, right) =>
      `${right.updatedAt || right.receivedAt || right.time || ''}`.localeCompare(
        `${left.updatedAt || left.receivedAt || left.time || ''}`,
      ),
    )
  const nextIds = new Set(activeMessages.map((message) => message.id))
  const hadActiveMessages = knownAbnormalIds.size > 0
  let hasNewMessage = false
  let latestMessage: AbnormalMessageItem | null = null

  activeMessages.forEach((message) => {
    if (!knownAbnormalIds.has(message.id)) {
      hasNewMessage = true
      if (
        !latestMessage ||
        `${message.updatedAt || message.receivedAt || message.time || ''}`.localeCompare(
          `${latestMessage.updatedAt || latestMessage.receivedAt || latestMessage.time || ''}`,
        ) > 0
      ) {
        latestMessage = message
      }
    }
  })

  knownAbnormalIds = nextIds

  if (!activeMessages.length && hadActiveMessages) {
    speechSynthesisRef?.cancel()
    stopPromptAudioPlayback()
    audioHint.value = '尾随异常已自动处理'
  }

  if (hasNewMessage && latestMessage && shouldBroadcastAbnormalMessage(latestMessage)) {
    broadcastAlarm(buildAbnormalBroadcastText(latestMessage), getAbnormalBroadcastOptions(latestMessage))
  }
}

async function closeAbnormalMessage(id: string) {
  if (!id) {
    return
  }

  try {
    const response = await fetch(buildApiUrl('/api/abnormal/close'), {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ id }),
    })

    if (!response.ok) {
      throw new Error(`异常消息关闭失败(${response.status})`)
    }

    if (!handledAbnormalIds.value.includes(id)) {
      handledAbnormalIds.value = [...handledAbnormalIds.value, id]
    }

    abnormalMessages.value = abnormalMessages.value.map((message) =>
      message.id === id
        ? {
            ...message,
            isHandled: true,
            status: '已处理',
            handledAt: new Date().toLocaleString(),
          }
        : message,
    )
    speechSynthesisRef?.cancel()
    stopPromptAudioPlayback()
    syncAbnormalState(abnormalMessages.value)
    audioHint.value = '异常消息已处理'
  } catch (error) {
    audioHint.value = error instanceof Error ? error.message : '异常消息关闭失败'
  }
}

function syncAlarmState(nextAlarms: AlarmItem[]) {
  const nextIds = new Set(nextAlarms.map((alarm) => alarm.id))
  let hasNewAlarm = false
  let latestAlarm: AlarmItem | null = null

  nextAlarms.forEach((alarm) => {
    if (!knownAlarmIds.has(alarm.id)) {
      hasNewAlarm = true
      latestAlarm ??= alarm
    }
  })

  knownAlarmIds = nextIds

  if (hasNewAlarm && latestAlarm) {
    broadcastAlarm(buildAlarmBroadcastText(latestAlarm), getAlarmBroadcastOptions(latestAlarm))
  }
}

async function loadDashboard() {
  try {
    const response = await fetch(buildApiUrl('/api/dashboard'), {
      headers: {
        Accept: 'application/json',
      },
    })

    if (!response.ok) {
      throw new Error(`接口返回 ${response.status}`)
    }

    const payload = (await response.json()) as DashboardResponse
    const nextAlarms = (payload.alarms ?? []).map(normalizeAlarm)
    const nextAbnormalMessages = (payload.abnormalMessages ?? []).map(normalizeAbnormalMessage)
    const nextAreaAlert = normalizeAreaAlert(payload.areaAlert)
    const previousAreaAlert = areaAlert.value

    metrics.value = payload.metrics?.length ? payload.metrics : metrics.value
    if (manualLimit.value === null) {
      syncLimitDraftFromMetrics()
      limitHint.value = formatLimitHint(null)
    }
    enterRecords.value = sortRecordsNewestFirst((payload.recentRecords ?? []).map(normalizeRecord))
    stayPeople.value = sortRecordsNewestFirst((payload.stayPeople ?? []).map(normalizeRecord))
    alarms.value = nextAlarms
    abnormalMessages.value = nextAbnormalMessages
    lastUpdatedAt.value = payload.generatedAt || new Date().toLocaleString()

    const fallbackId = enterRecords.value[0]?.id || stayPeople.value[0]?.id || ''
    const preferredId = payload.selectedRecordId || selectedRecordId.value || fallbackId
    const exists =
      enterRecords.value.some((record) => record.id === preferredId) ||
      stayPeople.value.some((record) => record.id === preferredId)

    selectedRecordId.value = exists ? preferredId : fallbackId
    syncAreaAlertState(previousAreaAlert, nextAreaAlert)
    syncAbnormalState(nextAbnormalMessages)
    syncAlarmState(visibleAlarms.value)
    loadError.value = ''
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : '加载数据失败'
  }
}

onMounted(() => {
  clockTimer = window.setInterval(() => {
    clock.value = new Date()
  }, 1000)

  const unlock = () => {
    void enableAlarmSound()
  }
  unlockAudioHandler = unlock
  window.addEventListener('pointerdown', unlock, { passive: true })
  window.addEventListener('touchend', unlock, { passive: true })
  window.addEventListener('click', unlock, { passive: true })
  window.addEventListener('keydown', unlock)

  loadDashboard()
  loadSavedLimit()
  void loadStayWarningMinutes()
  refreshTimer = window.setInterval(() => {
    loadDashboard()
  }, 3000)
})

onBeforeUnmount(() => {
  if (clockTimer) {
    window.clearInterval(clockTimer)
  }

  if (refreshTimer) {
    window.clearInterval(refreshTimer)
  }

  if (unlockAudioHandler) {
    window.removeEventListener('pointerdown', unlockAudioHandler)
    window.removeEventListener('touchend', unlockAudioHandler)
    window.removeEventListener('click', unlockAudioHandler)
    window.removeEventListener('keydown', unlockAudioHandler)
  }

  speechSynthesisRef?.cancel()
  stopPromptAudioPlayback()
  stopAreaAlertBroadcastLoop()

  if (audioContext) {
    void audioContext.close()
    audioContext = null
  }
})

const historyTotalPages = computed(() => {
  const total = historyTotalMatches.value
  if (total <= 0) {
    return 1
  }
  return Math.ceil(total / HISTORY_PAGE_SIZE)
})

const selectedRecord = computed(
  () =>
    enterRecords.value.find((record) => record.id === selectedRecordId.value) ??
    stayPeople.value.find((record) => record.id === selectedRecordId.value) ??
    historyEvents.value.find((record) => record.id === selectedRecordId.value) ??
    {
      ...emptyRecord,
      status: loadError.value || emptyRecord.status,
    },
)

const currentDate = computed(() => {
  const value = clock.value
  const year = value.getFullYear()
  const month = `${value.getMonth() + 1}`.padStart(2, '0')
  const day = `${value.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
})

const currentTime = computed(() => {
  const value = clock.value
  const hours = `${value.getHours()}`.padStart(2, '0')
  const minutes = `${value.getMinutes()}`.padStart(2, '0')
  const seconds = `${value.getSeconds()}`.padStart(2, '0')
  return `${hours}:${minutes}:${seconds}`
})

const currentWeekday = computed(() => {
  const labels = ['星期日', '星期一', '星期二', '星期三', '星期四', '星期五', '星期六']
  return labels[clock.value.getDay()]
})

const currentStayCount = computed(() => {
  const metric = metrics.value.find((item) => item.label === '区域内停留人员')
  if (metric) {
    const value = Number(metric.value)
    if (Number.isFinite(value)) {
      return Math.max(0, Math.floor(value))
    }
  }
  return stayPeople.value.length
})

const effectiveLimit = computed(() => (manualLimit.value === null ? getMetricValue('限制人数', 500) : manualLimit.value))
const remainingCapacity = computed(() => effectiveLimit.value - currentStayCount.value)
const accessRuleState = computed(() => {
  if (currentStayCount.value > effectiveLimit.value) {
    return {
      mode: 'blocked',
      title: '超员管控',
      summary: `当前 ${currentStayCount.value} 人，已超出限制人数 ${effectiveLimit.value} 人，只允许人员离开。`,
      enterLabel: '禁止进入',
      exitLabel: '允许离开',
    }
  }

  if (currentStayCount.value === effectiveLimit.value) {
    return {
      mode: 'exit-only',
      title: '只出不进',
      summary: `当前人数已达到限制人数 ${effectiveLimit.value} 人，允许出场，禁止进场。`,
      enterLabel: '禁止进入',
      exitLabel: '允许离开',
    }
  }

  if (remainingCapacity.value === 1) {
    return {
      mode: 'warning',
      title: '临界预警',
      summary: `当前人数距离限制人数仅差 1 人，请提前管控进场节奏。`,
      enterLabel: '允许进入',
      exitLabel: '允许离开',
    }
  }

  return {
    mode: 'normal',
    title: '正常通行',
    summary: `当前还可进入 ${Math.max(remainingCapacity.value, 0)} 人。`,
    enterLabel: '允许进入',
    exitLabel: '允许离开',
  }
})

const displayMetrics = computed(() => {
  const nextMetrics = metrics.value.map((item) => ({ ...item }))
  const limitIndex = nextMetrics.findIndex((item) => item.label === '限制人数')
  const stayIndex = nextMetrics.findIndex((item) => item.label === '区域内停留人员')

  if (limitIndex >= 0) {
    nextMetrics[limitIndex] = { ...nextMetrics[limitIndex], value: effectiveLimit.value }
  } else {
    nextMetrics.push({ label: '限制人数', value: effectiveLimit.value, unit: '人', accent: 'amber' })
  }

  if (stayIndex >= 0) {
    nextMetrics[stayIndex] = { ...nextMetrics[stayIndex], value: currentStayCount.value }
  } else {
    nextMetrics.push({ label: '区域内停留人员', value: currentStayCount.value, unit: '人', accent: 'lime' })
  }

  return nextMetrics
})

const capacityAlarms = computed(() => {
  const triggeredAt = lastUpdatedAt.value === '--' ? new Date().toLocaleString() : lastUpdatedAt.value

  if (currentStayCount.value > effectiveLimit.value) {
    return [
      normalizeAlarm({
        id: `capacity-exceeded-${effectiveLimit.value}-${currentStayCount.value}`,
        code: 'CAPACITY_EXCEEDED',
        category: 'capacity',
        level: '严重',
        title: '区域超员报警',
        detail: `区域内停留 ${currentStayCount.value} 人，已超出限制人数 ${effectiveLimit.value} 人，请立即疏导，仅允许出场。`,
        status: '立即处理',
        triggeredAt,
      }),
    ]
  }

  if (currentStayCount.value === effectiveLimit.value) {
    return [
      normalizeAlarm({
        id: `capacity-full-${effectiveLimit.value}`,
        code: 'CAPACITY_FULL',
        category: 'capacity',
        level: '高',
        title: '区域满员预警',
        detail: `区域人数已达限制人数 ${effectiveLimit.value} 人，当前可以出但不能进。`,
        status: '只出不进',
        triggeredAt,
      }),
    ]
  }

  if (remainingCapacity.value === 1) {
    return [
      normalizeAlarm({
        id: `capacity-near-limit-${effectiveLimit.value}-${currentStayCount.value}`,
        code: 'CAPACITY_NEAR_LIMIT',
        category: 'capacity',
        level: '中',
        title: '限员临界预警',
        detail: `区域内停留 ${currentStayCount.value} 人，距离限制人数 ${effectiveLimit.value} 仅差 1 人，请提前预警。`,
        status: '预警中',
        triggeredAt,
      }),
    ]
  }

  return []
})

const selectedPersonInfo = computed(() => [
  { label: '姓名', value: selectedRecord.value.name },
  { label: '工号', value: selectedRecord.value.id },
  { label: '部门', value: selectedRecord.value.department },
  { label: '岗位', value: selectedRecord.value.role },
  { label: '通行时间', value: selectedRecord.value.enterTime },
  { label: '通行方向', value: selectedRecord.value.direction },
  { label: '通行闸机', value: selectedRecord.value.gate },
  { label: '通行方式', value: selectedRecord.value.card },
 // { label: '当前位置', value: selectedRecord.value.location },
  ...(selectedRecord.value.stayDuration && selectedRecord.value.stayDuration !== '--'
    ? [{ label: '停留时长', value: selectedRecord.value.stayDuration }]
    : []),
  { label: '通行状态', value: selectedRecord.value.status },
])

const warningPersonIds = computed(
  () =>
    new Set([
      ...stayPeople.value
        .filter((person) => person.status === '停留预警')
        .map((person) => person.id),
      ...alarms.value.map((alarm) => alarm.targetId).filter(Boolean),
    ]),
)

const selectedPersonWarning = computed(() => warningPersonIds.value.has(selectedRecord.value.id))
const areaAlertActive = computed(() => {
  const currentId = areaAlert.value.alertId || `${areaAlert.value.zoneName}-${areaAlert.value.updatedAt}`
  return areaAlert.value.isActive && !handledAreaAlertIds.value.includes(currentId)
})
const areaAlertTitle = computed(() => (areaAlert.value.zoneName ? `${areaAlert.value.zoneName} 区域报警` : '区域报警'))
const areaAlertSummary = computed(() => {
  if (!areaAlertActive.value) {
    return '当前无区域报警'
  }
  return `${areaAlert.value.zoneName || '未知区域'} 检测到 ${areaAlert.value.hasPeople} 人，请立即处理`
})
const activeAbnormalMessages = computed(() =>
  [...abnormalMessages.value]
    .filter((message) => !message.isHandled && !handledAbnormalIds.value.includes(message.id))
    .sort((left, right) =>
      `${right.updatedAt || right.receivedAt || right.time || ''}`.localeCompare(
        `${left.updatedAt || left.receivedAt || left.time || ''}`,
      ),
    ),
)
const visibleAbnormalMessages = computed(() => activeAbnormalMessages.value)
const abnormalWindowTitle = computed(() =>
  visibleAbnormalMessages.value.length ? `异常行为告警 ${visibleAbnormalMessages.value.length} 条` : '异常行为告警',
)
const visibleAlarms = computed(() => {
  const seenIds = new Set<string>()
  return [...capacityAlarms.value, ...alarms.value]
    .filter((alarm) => !(alarm.category === 'area' && handledAreaAlertIds.value.includes(alarm.id)))
    .filter((alarm) => {
      if (seenIds.has(alarm.id)) {
        return false
      }
      seenIds.add(alarm.id)
      return true
    })
})
const activeAlarmSummary = computed(() => {
  if (!visibleAlarms.value.length) {
    return '当前无活动报警'
  }
  return visibleAlarms.value[0].detail
})
const audioButtonLabel = computed(() => {
  if (!audioEnabled.value) {
    return '开启语音播报'
  }
  if (audioMode.value === 'speech') {
    return '语音播报已开启'
  }
  if (audioMode.value === 'prompt') {
    return '录音播报已开启'
  }
  return '提示音已开启'
})
</script>
                    
<template>
  <main class="screen" :class="{ 'screen--area-alert-active': areaAlertActive }">
    <div v-if="areaAlertActive" class="alert-overlay"></div>
    <div v-if="activeAbnormalMessages.length" class="abnormal-overlay"></div>
    <div v-if="historyVisible" class="history-overlay" @click="closeHistoryPanel"></div>
    <header class="topbar panel">
      <div>
        <p class="topbar__tag">PERSONNEL ACCESS CONTROL PLATFORM</p>
        <h1>{{ systemTitle }}</h1>
      </div>
      <div class="topbar__meta">
        <span class="status-chip" :class="{ 'status-chip--alarm': areaAlertActive }">
          <i></i>
          {{ loadError ? '接口异常' : areaAlertActive ? '区域报警' : activeAbnormalMessages.length ? '异常消息' : '实时监测' }}
        </span>
        <button class="sound-button" type="button" :class="{ 'sound-button--active': audioEnabled }" @click="enableAlarmSound">
          {{ audioButtonLabel }}
        </button>
        <span class="hint-chip">{{ audioHint }}</span>
      </div>
    </header>

    <section v-if="areaAlertActive" class="area-alert-banner panel">
      <div class="alarm-banner__label">AREA ALERT</div>
      <div class="area-alert-banner__top">
        <strong>{{ areaAlertTitle }}</strong>
        <button class="area-alert-banner__action" type="button" @click="handleAreaAlert">处理</button>
      </div>
      <p>{{ areaAlertSummary }}</p>
      <small>{{ areaAlert.triggeredAt || areaAlert.updatedAt }}</small>
    </section>

    <section v-if="visibleAlarms.length" class="alarm-banner panel">
      <div class="alarm-banner__label">活动报警</div>
      <strong>{{ visibleAlarms[0].title }}</strong>
      <p>{{ activeAlarmSummary }}</p>
    </section>

    <section v-if="historyVisible" class="history-window panel" @click.stop>
      <div class="history-window__head">
        <div>
          <p class="eyebrow">EVENT HISTORY</p>
          <h2>历史门禁事件</h2>
        </div>
        <button class="history-window__close" type="button" @click="closeHistoryPanel">关闭</button>
      </div>

      <div class="history-window__filters">
        <label class="history-window__field">
          <span>门禁设备</span>
          <select v-model="historyDeviceIP" class="history-window__input">
            <option v-if="!historyDevices.length" value="">暂无可用设备</option>
            <option v-for="device in historyDevices" :key="device.ip" :value="device.ip">
              {{ device.deviceName || device.name || device.ip }}（{{ device.ip }}）
            </option>
          </select>
        </label>
        <label class="history-window__field history-window__field--range">
          <span>开始时间</span>
          <div class="history-window__datetime">
            <input v-model="historyStartDate" class="history-window__input" type="date" />
            <input v-model="historyStartClock" class="history-window__input" type="time" step="60" />
          </div>
        </label>
        <label class="history-window__field history-window__field--range">
          <span>结束时间</span>
          <div class="history-window__datetime">
            <input v-model="historyEndDate" class="history-window__input" type="date" />
            <input v-model="historyEndClock" class="history-window__input" type="time" step="60" />
          </div>
        </label>
        <button
          class="history-window__search"
          type="button"
          :disabled="historyLoading || !historyDevices.length"
          @click="searchHistoryEvents"
        >
          {{ historyLoading ? '查询中...' : '查询历史' }}
        </button>
      </div>

      <div class="history-window__presets">
        <span class="history-window__presets-label">快捷范围</span>
        <button class="history-window__preset" type="button" @click="applyHistoryPreset('today')">今天</button>
        <button class="history-window__preset" type="button" @click="applyHistoryPreset('yesterday')">昨天</button>
        <button class="history-window__preset" type="button" @click="applyHistoryPreset('last7')">近7天</button>
        <button class="history-window__preset" type="button" @click="applyHistoryPreset('last30')">近30天</button>
      </div>

      <div class="history-window__meta">
        <span class="hint-chip">共 {{ historyTotalMatches }} 条，每页 {{ HISTORY_PAGE_SIZE }} 条</span>
        <span v-if="historyError" class="history-window__error">{{ historyError }}</span>
      </div>

      <div ref="historyListRef" class="history-window__list records-list">
        <button
          v-for="record in historyEvents"
          :key="`${record.id}-${record.enterTime}-${record.gate}`"
          class="record-row"
          :class="{ 'record-row--active': selectedRecordId === record.id }"
          type="button"
          @click="selectedRecordId = record.id"
        >
          <div class="record-row__avatar">
            <img
              v-if="record.imageUrl"
              class="record-row__avatar-image"
              :src="resolveImageUrl(record.imageUrl)"
              :alt="record.name"
              loading="lazy"
              decoding="async"
            />
            <span v-else>{{ record.avatarText }}</span>
          </div>
          <div class="record-row__main">
            <strong>{{ record.name }}</strong>
            <span>{{ record.role }}</span>
          </div>
          <div class="record-row__time">
            <span>{{ record.enterTime }}</span>
            <div class="record-row__tags">
              <em
                class="record-direction-badge"
                :class="{
                  'record-direction-badge--in': record.direction === '进',
                  'record-direction-badge--out': record.direction === '出',
                }"
              >
                {{ record.direction }}
              </em>
              <em class="history-type-badge">{{ record.status }}</em>
            </div>
          </div>
        </button>
        <div v-if="historyLoading" class="empty-state">正在查询历史事件...</div>
        <div v-else-if="!historyEvents.length" class="empty-state">选择时间范围后点击查询历史</div>
      </div>

      <div v-if="historyTotalMatches > 0" class="history-window__pagination">
        <button
          class="history-window__page-btn"
          type="button"
          :disabled="historyLoading || historyPage <= 1"
          @click="goToHistoryPage(historyPage - 1)"
        >
          上一页
        </button>
        <span class="history-window__page-info">第 {{ historyPage }} / {{ historyTotalPages }} 页</span>
        <button
          class="history-window__page-btn"
          type="button"
          :disabled="historyLoading || historyPage >= historyTotalPages"
          @click="goToHistoryPage(historyPage + 1)"
        >
          下一页
        </button>
      </div>
    </section>

    <section v-if="activeAbnormalMessages.length" class="abnormal-window panel">
      <div class="abnormal-window__head">
        <div>
          <div class="alarm-banner__label">ABNORMAL MQTT</div>
          <strong>{{ abnormalWindowTitle }}</strong>
        </div>
      </div>
      <div class="abnormal-window__list">
        <article v-for="message in visibleAbnormalMessages.slice(0, 5)" :key="message.id" class="abnormal-window__card">
          <div class="abnormal-window__card-top">
            <div>
              <strong>{{ formatAbnormalMessageTitle(message) }}</strong>
              <p>{{ message.time || message.receivedAt || '--' }}</p>
            </div>
            <button class="abnormal-window__action" type="button" @click="closeAbnormalMessage(message.id)">
              处理关闭
            </button>
          </div>
          <small>{{ message.status || '待处理' }}</small>
        </article>
      </div>
    </section>

    <section class="content-grid">
      <div class="left-stack">
        <section class="panel metrics-panel">
          <div class="panel-head">
            <div>
              <p class="eyebrow">CORE OVERVIEW</p>
              <h2>核心数据</h2>
            </div>
            <span class="hint-chip">最后更新 {{ lastUpdatedAt }}</span>
          </div>

          <div class="metrics-grid">
            <article
              v-for="item in displayMetrics"
              :key="item.label"
              class="metric-card"
              :class="`metric-card--${item.accent}`"
            >
              <span class="metric-card__label">{{ item.label }}</span>
              <div class="metric-card__value">
                <strong>{{ item.value }}</strong>
                <small>{{ item.unit }}</small>
              </div>
            </article>
          </div>

          <div class="capacity-control">
            <div class="capacity-control__form">
              <label class="capacity-control__label" for="limit-input">前端限制人数</label>
              <input
                id="limit-input"
                v-model="limitDraft"
                class="capacity-control__input"
                type="number"
                min="1"
                step="1"
                inputmode="numeric"
                @focus="isEditingLimit = true"
                @blur="isEditingLimit = false"
              />
              <button class="capacity-control__button" type="button" @click="applyManualLimit">应用</button>
              <span class="hint-chip">{{ effectiveLimit }} 人</span>
            </div>

            <div class="capacity-control__form">
              <label class="capacity-control__label" for="stay-warning-input">停留报警时间</label>
              <input
                id="stay-warning-input"
                v-model="stayWarningMinutesDraft"
                class="capacity-control__input"
                type="number"
                min="1"
                step="1"
                inputmode="numeric"
                @focus="isEditingStayWarningMinutes = true"
                @blur="isEditingStayWarningMinutes = false"
              />
              <button class="capacity-control__button" type="button" @click="applyStayWarningMinutes">应用</button>
              <span class="hint-chip">{{ stayWarningMinutes === null ? '--' : `${stayWarningMinutes} 分钟` }}</span>
            </div>

            <div class="capacity-control__meta">
              <span class="hint-chip">{{ limitHint }}</span>
              <span class="hint-chip">{{ stayWarningHint }}</span>
              <span class="hint-chip">当前停留 {{ currentStayCount }} / {{ effectiveLimit }}</span>
              <span class="hint-chip" :class="`pass-rule-chip--${accessRuleState.mode}`">{{ accessRuleState.title }}</span>
            </div>

            <div class="pass-rule-card" :class="`pass-rule-card--${accessRuleState.mode}`">
              <div>
                <p class="eyebrow">ACCESS RULE</p>
                <h3>{{ accessRuleState.title }}</h3>
                <p class="pass-rule-card__summary">{{ accessRuleState.summary }}</p>
              </div>
              <div class="pass-rule-card__tags">
                <span class="pass-rule-chip" :class="`pass-rule-chip--${accessRuleState.mode}`">进场：{{ accessRuleState.enterLabel }}</span>
                <span class="pass-rule-chip pass-rule-chip--exit">出场：{{ accessRuleState.exitLabel }}</span>
              </div>
            </div>
          </div>
        </section>

        <section class="panel alarm-panel">
          <div class="panel-head">
            <div>
              <p class="eyebrow">ALARM CENTER</p>
              <h2>报警信息</h2>
            </div>
            <span class="alarm-chip">实时告警 {{ visibleAlarms.length }} 条</span>
          </div>

          <div class="alarm-list">
            <button
              v-for="alarm in visibleAlarms"
              :key="alarm.id"
              class="alarm-card"
              :class="{ 'alarm-card--link': !!alarm.targetId }"
              type="button"
              @click="alarm.targetId && (selectedRecordId = alarm.targetId)"
            >
              <div class="alarm-card__icon">!</div>
              <div class="alarm-card__content">
                <div class="alarm-card__top">
                  <strong>{{ alarm.title }}</strong>
                  <span>{{ alarm.level }}</span>
                </div>
                <p>{{ alarm.detail }}</p>
                <small v-if="alarm.targetName || alarm.gate || alarm.triggeredAt" class="alarm-card__meta">
                  {{ alarm.targetName || '全局报警' }}
                  <template v-if="alarm.gate"> · {{ alarm.gate }}</template>
                  <template v-if="alarm.triggeredAt"> · {{ alarm.triggeredAt }}</template>
                </small>
              </div>
              <div class="alarm-card__status">{{ alarm.status }}</div>
            </button>
            <div v-if="!visibleAlarms.length" class="empty-state">当前没有报警信息</div>
          </div>
        </section>

        <section class="panel person-panel">
          <div class="panel-head">
            <div>
              <p class="eyebrow">PERSON INFO</p>
              <h2>人员信息</h2>
            </div>
            <span class="hint-chip">{{ loadError || `点击右侧记录切换` }}</span>
          </div>

          <div class="person-card">
            <div class="person-avatar">
              <div class="person-avatar__halo"></div>
              <img
                v-if="selectedRecord.imageUrl"
                class="person-avatar__image"
                :src="resolveImageUrl(selectedRecord.imageUrl)"
                :alt="selectedRecord.name"
              />
              <div v-else class="person-avatar__body">{{ selectedRecord.avatarText }}</div>
            </div>

            <div class="person-summary">
              <div class="person-summary__top">
                <h3>{{ selectedRecord.name }}</h3>
                <span v-if="selectedPersonWarning" class="person-alert-tag">重点关注</span>
              </div>
              <p>{{ selectedRecord.role }}</p>
              <p>{{ selectedRecord.department }}</p>
              <p>{{ selectedRecord.gate }}</p>
            </div>
          </div>

          <div class="person-info-grid">
            <div v-for="item in selectedPersonInfo" :key="item.label" class="person-info-row">
              <span>{{ item.label }}</span>
              <strong>{{ item.value }}</strong>
            </div>
          </div>
        </section>
      </div>

      <div class="right-stack">
        <section class="panel records-panel">
          <div class="panel-head">
            <div>
              <p class="eyebrow">RECENT PASSES</p>
              <h2>最近10条通行记录</h2>
            </div>
            <div class="records-panel__actions">
              <button class="history-open-button" type="button" @click="openHistoryPanel">查看历史</button>
              <span class="hint-chip">{{ enterRecords.length }} 条记录</span>
            </div>
          </div>

          <div class="records-list">
            <button
              v-for="record in enterRecords"
              :key="`${record.id}-${record.enterTime}`"
              class="record-row"
              :class="{
                'record-row--active': selectedRecordId === record.id,
                'record-row--warn': warningPersonIds.has(record.id),
              }"
              type="button"
              @click="selectedRecordId = record.id"
            >
              <div class="record-row__avatar">
                <img
                  v-if="record.imageUrl"
                  class="record-row__avatar-image"
                  :src="resolveImageUrl(record.imageUrl)"
                  :alt="record.name"
                />
                <span v-else>{{ record.avatarText }}</span>
              </div>
              <div class="record-row__main">
                <strong>{{ record.name }}</strong>
                <span>{{ record.department }}</span>
              </div>
              <div class="record-row__time">
                <span>{{ record.enterTime }}</span>
                <div class="record-row__tags">
                  <em
                    class="record-direction-badge"
                    :class="{
                      'record-direction-badge--in': record.direction === '进',
                      'record-direction-badge--out': record.direction === '出',
                    }"
                  >
                    {{ record.direction }}
                  </em>
                  <em v-if="warningPersonIds.has(record.id)" class="record-warn-badge">预警</em>
                </div>
              </div>
            </button>
            <div v-if="!enterRecords.length" class="empty-state">还没有收到通行记录</div>
          </div>
        </section>

        <section class="panel stay-panel">
          <div class="panel-head">
            <div>
              <p class="eyebrow">STAYING PEOPLE</p>
              <h2>停留人员名单</h2>
            </div>
            <span class="hint-chip">{{ stayPeople.length }} 人</span>
          </div>

          <div class="stay-list">
            <div class="stay-table-head">
              <span>姓名</span>
              <span>部门</span>
              <span>通行闸机</span>
              <span>停留时长</span>
            </div>

            <button
              v-for="person in stayPeople"
              :key="`${person.id}-${person.stayDuration}`"
              class="stay-row"
              :class="{
                'stay-row--active': selectedRecordId === person.id,
                'stay-row--warn': warningPersonIds.has(person.id),
              }"
              type="button"
              @click="selectedRecordId = person.id"
            >
              <strong class="stay-row__name">{{ person.name }}</strong>
              <span class="stay-row__dept">{{ person.department }}</span>
              <span class="stay-row__location">{{ person.gate !== '--' ? person.gate : person.location }}</span>
              <div class="stay-row__meta">
                <strong>{{ person.stayDuration || '--' }}</strong>
                <em :class="{ 'stay-status--warn': person.status === '停留预警' }">{{ person.status }}</em>
              </div>
            </button>
            <div v-if="!stayPeople.length" class="empty-state">当前区域没有停留人员</div>
          </div>
        </section>
      </div>
    </section>

    <footer class="footer panel">
      <div class="footer__item">
        <span>日期</span>
        <strong>{{ currentDate }}</strong>
      </div>
      <div class="footer__item">
        <span>星期</span>
        <strong>{{ currentWeekday }}</strong>
      </div>
      <div class="footer__item footer__item--time">
        <span>时间</span>
        <strong>{{ currentTime }}</strong>
      </div>
    </footer>
  </main>
</template>

<style scoped>
.screen {
  min-height: 100vh;
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr) auto;
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.screen > * {
  position: relative;
  z-index: 1;
}

.alert-overlay {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 0;
  background:
    radial-gradient(circle at 50% 20%, rgba(255, 72, 72, 0.3), transparent 55%),
    linear-gradient(180deg, rgba(255, 28, 28, 0.18), rgba(120, 0, 0, 0.12));
  animation: area-alert-overlay 1.1s infinite;
}

.abnormal-overlay {
  position: fixed;
  inset: 0;
  z-index: 3;
  background:
    radial-gradient(circle at center, rgba(255, 82, 82, 0.18), rgba(0, 0, 0, 0.62) 64%),
    rgba(4, 10, 18, 0.42);
  backdrop-filter: blur(3px);
}

.history-overlay {
  position: fixed;
  inset: 0;
  z-index: 4;
  background:
    radial-gradient(circle at center, rgba(66, 173, 255, 0.14), rgba(0, 0, 0, 0.68) 64%),
    rgba(4, 10, 18, 0.48);
  backdrop-filter: blur(4px);
}

.history-window {
  position: fixed;
  top: 50%;
  left: 50%;
  z-index: 6;
  transform: translate(-50%, -50%);
  width: min(980px, calc(100vw - 28px));
  max-height: min(82vh, 860px);
  display: grid;
  grid-template-rows: auto auto auto auto minmax(0, 1fr) auto;
  gap: 12px;
  border-color: rgba(92, 201, 255, 0.42);
}

.history-window__head,
.history-window__filters,
.history-window__meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.history-window__head {
  align-items: start;
}

.history-window__head h2 {
  margin: 4px 0 0;
  font-size: 24px;
}

.history-window__close,
.history-open-button,
.history-window__search {
  height: 38px;
  padding: 0 16px;
  border: 1px solid rgba(92, 201, 255, 0.22);
  border-radius: 12px;
  cursor: pointer;
  color: #ecfbff;
  background: linear-gradient(180deg, rgba(29, 141, 255, 0.28), rgba(13, 63, 130, 0.38));
}

.history-window__close:disabled,
.history-window__search:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.history-window__filters {
  display: grid;
  grid-template-columns: minmax(180px, 1fr) repeat(2, minmax(220px, 1.1fr)) auto;
  align-items: end;
  gap: 12px;
}

.history-window__field {
  display: grid;
  gap: 6px;
  font-size: 12px;
  color: #9edfff;
}

.history-window__datetime {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(108px, 0.85fr);
  gap: 8px;
}

.history-window__input {
  width: 100%;
  min-width: 0;
  height: 38px;
  padding: 0 12px;
  border-radius: 12px;
  border: 1px solid rgba(92, 201, 255, 0.18);
  outline: none;
  color: #f3fbff;
  background: rgba(3, 14, 28, 0.92);
  color-scheme: dark;
}

.history-window__input::-webkit-calendar-picker-indicator {
  cursor: pointer;
  opacity: 0.85;
  filter: invert(0.82) sepia(0.2) saturate(4) hue-rotate(160deg);
}

.history-window__presets {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.history-window__presets-label {
  font-size: 12px;
  color: #9edfff;
  margin-right: 4px;
}

.history-window__preset {
  height: 32px;
  padding: 0 12px;
  border: 1px solid rgba(92, 201, 255, 0.18);
  border-radius: 999px;
  cursor: pointer;
  color: #d8f4ff;
  background: rgba(66, 173, 255, 0.1);
}

.history-window__meta {
  justify-content: flex-start;
  flex-wrap: wrap;
}

.history-window__error {
  color: #ff9f9f;
  font-size: 13px;
}

.history-window__list {
  min-height: 0;
  overflow: auto;
}

.history-window__pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding-top: 4px;
}

.history-window__page-btn {
  height: 34px;
  padding: 0 14px;
  border: 1px solid rgba(92, 201, 255, 0.22);
  border-radius: 12px;
  cursor: pointer;
  color: #ecfbff;
  background: linear-gradient(180deg, rgba(29, 141, 255, 0.28), rgba(13, 63, 130, 0.38));
}

.history-window__page-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.history-window__page-info {
  min-width: 120px;
  text-align: center;
  font-size: 13px;
  color: #d8f4ff;
}

.records-panel__actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.history-type-badge {
  font-style: normal;
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 999px;
  color: #d8f4ff;
  background: rgba(66, 173, 255, 0.16);
  border: 1px solid rgba(92, 201, 255, 0.18);
}

.panel {
  border: 1px solid rgba(92, 201, 255, 0.18);
  border-radius: 24px;
  padding: 14px;
  background:
    linear-gradient(180deg, rgba(15, 38, 67, 0.94), rgba(6, 18, 35, 0.94)),
    linear-gradient(135deg, rgba(61, 185, 255, 0.08), transparent 45%);
  box-shadow:
    0 0 0 1px rgba(66, 173, 255, 0.08) inset,
    0 18px 40px rgba(0, 0, 0, 0.24);
  backdrop-filter: blur(12px);
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.topbar__tag,
.eyebrow {
  margin: 0;
  font-size: 11px;
  letter-spacing: 0.34em;
  text-transform: uppercase;
  color: #78d9ff;
}

.topbar__meta {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 10px;
}

.area-alert-banner {
  display: grid;
  gap: 6px;
  border-color: rgba(255, 70, 70, 0.64);
  background:
    linear-gradient(180deg, rgba(126, 8, 8, 0.96), rgba(66, 4, 4, 0.92)),
    linear-gradient(135deg, rgba(255, 133, 133, 0.2), transparent 55%);
  box-shadow: 0 0 36px rgba(255, 61, 61, 0.22);
  animation: area-alert-banner 0.9s infinite alternate;
}

.abnormal-window {
  position: fixed;
  top: 50%;
  left: 50%;
  z-index: 5;
  transform: translate(-50%, -50%);
  width: min(520px, calc(100vw - 28px));
  max-height: min(72vh, 680px);
  display: grid;
  gap: 10px;
  border-color: rgba(255, 92, 92, 0.58);
  background:
    linear-gradient(180deg, rgba(92, 10, 18, 0.97), rgba(46, 6, 10, 0.94)),
    linear-gradient(135deg, rgba(255, 122, 122, 0.18), transparent 55%);
  box-shadow:
    0 28px 80px rgba(0, 0, 0, 0.46),
    0 0 42px rgba(255, 61, 61, 0.24),
    0 0 0 1px rgba(255, 184, 184, 0.1) inset;
}

.abnormal-window__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.abnormal-window__head strong {
  color: #fff0f0;
  font-size: 18px;
}

.abnormal-window__list {
  display: grid;
  gap: 8px;
  max-height: min(52vh, 480px);
  overflow-y: auto;
  padding-right: 4px;
}

.abnormal-window__card {
  display: grid;
  gap: 6px;
  padding: 12px;
  border-radius: 18px;
  border: 1px solid rgba(255, 122, 122, 0.24);
  background: rgba(86, 14, 20, 0.74);
}

.abnormal-window__card-top {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.abnormal-window__card-top strong {
  color: #fff1f1;
  font-size: 15px;
}

.abnormal-window__card-top p,
.abnormal-window__card small {
  color: rgba(255, 205, 205, 0.84);
  font-size: 12px;
}

.abnormal-window__action {
  border: 1px solid rgba(255, 220, 220, 0.3);
  background: rgba(255, 255, 255, 0.12);
  color: #fff7f7;
  border-radius: 999px;
  padding: 7px 12px;
  cursor: pointer;
  font-size: 12px;
  white-space: nowrap;
}

.abnormal-window__action:hover {
  background: rgba(255, 255, 255, 0.16);
}

.abnormal-window__status {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  padding: 7px 12px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 220, 220, 0.18);
  color: rgba(255, 235, 235, 0.88);
  font-size: 12px;
  white-space: nowrap;
}

.abnormal-window__payload {
  margin: 0;
  padding: 10px 12px;
  border-radius: 14px;
  overflow-x: auto;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
  line-height: 1.5;
  color: #ffe3e3;
  background: rgba(28, 6, 9, 0.58);
  border: 1px solid rgba(255, 145, 145, 0.14);
}

.area-alert-banner__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.area-alert-banner__action {
  border: 1px solid rgba(255, 233, 233, 0.3);
  background: rgba(255, 255, 255, 0.12);
  color: #fff7f7;
  border-radius: 999px;
  padding: 8px 14px;
  cursor: pointer;
  font-size: 12px;
}

.area-alert-banner__action:hover {
  background: rgba(255, 255, 255, 0.2);
}

.alarm-banner {
  display: grid;
  gap: 6px;
  border-color: rgba(255, 113, 113, 0.32);
  background:
    linear-gradient(180deg, rgba(91, 10, 21, 0.9), rgba(54, 5, 10, 0.88)),
    linear-gradient(135deg, rgba(255, 133, 133, 0.15), transparent 55%);
}

.alarm-banner__label {
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: #ffb8b8;
}

.alarm-banner strong {
  color: #fff4f4;
  font-size: 18px;
}

.alarm-banner p {
  color: rgba(255, 221, 221, 0.88);
  font-size: 13px;
}

.sound-button,
.status-chip,
.hint-chip,
.alarm-chip {
  color: #ffd0d0;
  background: rgba(138, 31, 31, 0.28);
  border-color: rgba(255, 113, 113, 0.22);
}

.status-chip--alarm {
  color: #fff0f0;
  background: rgba(156, 24, 24, 0.44);
  border-color: rgba(255, 94, 94, 0.42);
  box-shadow: 0 0 24px rgba(255, 71, 71, 0.22);
}

.status-chip i {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #4cffb2;
  box-shadow: 0 0 10px rgba(76, 255, 178, 0.8);
}

h1,
h2,
h3,
p {
  margin: 0;
}

h1 {
  font-size: clamp(22px, 2vw, 32px);
  letter-spacing: 0.12em;
  color: #f3fbff;
}

h2 {
  font-size: 17px;
  color: #f3fbff;
}

h3 {
  font-size: 22px;
  color: #f3fbff;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(360px, 0.9fr);
  gap: 12px;
  min-height: 0;
}

.left-stack {
  display: grid;
  grid-template-rows: minmax(400px, 1.08fr) minmax(170px, 0.62fr) minmax(250px, 0.92fr);
  gap: 12px;
  min-height: 0;
}

.right-stack {
  display: grid;
  grid-template-rows: minmax(0, 1.08fr) minmax(180px, 0.78fr);
  gap: 12px;
  min-height: 0;
}

.metrics-panel,
.person-panel,
.records-panel,
.stay-panel,
.left-stack,
.right-stack {
  min-height: 0;
}

.metrics-panel,
.alarm-panel,
.records-panel,
.stay-panel {
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
}

.metrics-panel {
  display: grid;
  grid-template-rows: auto auto auto;
  align-content: start;
  gap: 12px;
}

.person-panel {
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
}

.panel-head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  min-height: 0;
  grid-auto-rows: minmax(102px, 1fr);
}

.capacity-control {
  display: grid;
  gap: 12px;
  padding: 14px;
  border-radius: 20px;
  background: rgba(7, 22, 40, 0.86);
  border: 1px solid rgba(92, 201, 255, 0.12);
}

.capacity-control__form {
  display: grid;
  grid-template-columns: auto minmax(120px, 180px) auto auto;
  align-items: center;
  gap: 10px;
}

.capacity-control__label {
  font-size: 13px;
  color: #d5f8ff;
  white-space: nowrap;
}

.capacity-control__input {
  width: 100%;
  min-width: 0;
  height: 38px;
  padding: 0 12px;
  border-radius: 12px;
  border: 1px solid rgba(92, 201, 255, 0.18);
  outline: none;
  color: #f3fbff;
  background: rgba(3, 14, 28, 0.92);
  box-shadow: inset 0 0 0 1px rgba(66, 173, 255, 0.05);
}

.capacity-control__input:focus {
  border-color: rgba(92, 201, 255, 0.46);
  box-shadow: 0 0 0 3px rgba(66, 173, 255, 0.12);
}

.capacity-control__button {
  height: 38px;
  padding: 0 16px;
  border: 1px solid rgba(92, 201, 255, 0.22);
  border-radius: 12px;
  cursor: pointer;
  color: #ecfbff;
  background: linear-gradient(180deg, rgba(29, 141, 255, 0.28), rgba(13, 63, 130, 0.38));
}

.capacity-control__button--ghost {
  background: rgba(255, 255, 255, 0.05);
}

.capacity-control__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.pass-rule-card {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 14px;
  align-items: center;
  padding: 16px 18px;
  border-radius: 18px;
  border: 1px solid rgba(92, 201, 255, 0.16);
  background:
    linear-gradient(180deg, rgba(12, 34, 62, 0.92), rgba(5, 16, 30, 0.95)),
    linear-gradient(135deg, rgba(61, 185, 255, 0.08), transparent 55%);
}

.pass-rule-card--normal {
  border-color: rgba(92, 201, 255, 0.16);
}

.pass-rule-card--warning {
  border-color: rgba(255, 201, 92, 0.28);
  background:
    linear-gradient(180deg, rgba(54, 41, 9, 0.92), rgba(32, 23, 5, 0.95)),
    linear-gradient(135deg, rgba(255, 207, 101, 0.12), transparent 55%);
}

.pass-rule-card--exit-only,
.pass-rule-card--blocked {
  border-color: rgba(255, 113, 113, 0.32);
  background:
    linear-gradient(180deg, rgba(63, 14, 20, 0.92), rgba(36, 7, 11, 0.95)),
    linear-gradient(135deg, rgba(255, 133, 133, 0.16), transparent 55%);
}

.pass-rule-card__summary {
  margin-top: 8px;
  font-size: 14px;
  line-height: 1.6;
  color: rgba(219, 244, 255, 0.88);
}

.pass-rule-card__tags {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.pass-rule-chip {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 32px;
  padding: 4px 12px;
  border-radius: 999px;
  font-size: 12px;
  color: #d7ffe9;
  background: rgba(41, 176, 104, 0.2);
  border: 1px solid rgba(117, 255, 176, 0.22);
}

.pass-rule-chip--normal {
  color: #d7ffe9;
  background: rgba(41, 176, 104, 0.2);
  border-color: rgba(117, 255, 176, 0.22);
}

.pass-rule-chip--warning {
  color: #fff2d8;
  background: rgba(210, 141, 56, 0.22);
  border-color: rgba(255, 196, 118, 0.22);
}

.pass-rule-chip--exit-only,
.pass-rule-chip--blocked {
  color: #ffdcdc;
  background: rgba(156, 24, 24, 0.32);
  border-color: rgba(255, 94, 94, 0.3);
}

.pass-rule-chip--exit {
  color: #d9fbff;
  background: rgba(44, 139, 255, 0.12);
  border-color: rgba(91, 230, 255, 0.14);
}

.metric-card {
  position: relative;
  overflow: hidden;
  min-height: 0;
  padding: 14px 14px 12px;
  border-radius: 20px;
  background: linear-gradient(180deg, rgba(5, 18, 28, 0.94), rgba(2, 10, 16, 0.98));
  border: 1px solid rgba(89, 210, 255, 0.16);
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
}

.metric-card::before {
  content: '';
  position: absolute;
  inset: 0;
  background:
    linear-gradient(transparent 92%, rgba(102, 255, 209, 0.08) 92%),
    linear-gradient(90deg, transparent 92%, rgba(102, 255, 209, 0.08) 92%);
  background-size: 24px 24px;
  opacity: 0.4;
}

.metric-card__label,
.metric-card__value {
  position: relative;
  z-index: 1;
}

.metric-card__label {
  display: block;
  min-height: 1.5em;
  font-size: 15px;
  line-height: 1.4;
  color: rgba(211, 252, 255, 0.92);
}

.metric-card__value {
  margin-top: auto;
  display: flex;
  align-items: end;
  gap: 6px;
  padding-top: 8px;
  padding-bottom: 2px;
}

.metric-card__value strong {
  font-size: clamp(28px, 3vw, 42px);
  line-height: 1.08;
  text-shadow: 0 0 16px currentColor;
}

.metric-card__value small {
  font-size: 13px;
  margin-bottom: 3px;
  color: rgba(216, 243, 255, 0.84);
}

.metric-card--cyan .metric-card__value strong {
  color: #6ee8ff;
}

.metric-card--teal .metric-card__value strong {
  color: #57ffd6;
}

.metric-card--amber .metric-card__value strong {
  color: #ffcf65;
}

.metric-card--lime .metric-card__value strong {
  color: #89ff79;
}

.alarm-list,
.records-list,
.stay-list {
  min-height: 0;
  overflow-y: auto;
  display: grid;
  align-content: start;
  grid-auto-rows: max-content;
  gap: 8px;
  padding: 6px 4px 4px 2px;
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(9, 27, 48, 0.96), rgba(6, 19, 35, 0.94));
  box-shadow: inset 0 0 0 1px rgba(92, 201, 255, 0.12);
}

.alarm-card {
  display: grid;
  grid-template-columns: 48px minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 18px;
  border: 1px solid rgba(255, 92, 92, 0.24);
  background: linear-gradient(180deg, rgba(91, 10, 21, 0.84), rgba(54, 5, 10, 0.88));
  box-shadow: 0 0 20px rgba(255, 89, 89, 0.12);
  width: 100%;
  text-align: left;
  cursor: default;
}

.alarm-card--link {
  cursor: pointer;
}

.alarm-card--link:hover,
.record-row:hover,
.record-row--active,
.stay-row:hover,
.stay-row--active {
  transform: translateY(-1px);
}

.alarm-card__icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  border-radius: 16px;
  font-size: 24px;
  font-weight: 700;
  color: #240709;
  background: linear-gradient(180deg, #ff8881, #ff4747);
}

.alarm-card__content {
  display: grid;
  gap: 4px;
}

.alarm-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.alarm-card__top strong {
  font-size: 15px;
  color: #fff1f1;
}

.alarm-card__top span,
.alarm-card__meta {
  font-size: 11px;
  color: #ffb4b4;
}

.alarm-card__content p {
  font-size: 12px;
  color: rgba(255, 222, 222, 0.86);
}

.alarm-card__status {
  padding: 5px 10px;
  border-radius: 999px;
  font-size: 11px;
  color: #fff2f2;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 185, 185, 0.18);
}

.person-card {
  display: grid;
  grid-template-columns: 88px minmax(0, 1fr);
  gap: 12px;
  padding: 12px;
  border-radius: 20px;
  background: rgba(10, 30, 56, 0.72);
  border: 1px solid rgba(85, 200, 255, 0.14);
  margin-bottom: 10px;
}

.person-avatar {
  position: relative;
  min-height: 96px;
  display: grid;
  place-items: center;
}

.person-avatar__halo {
  position: absolute;
  inset: 8px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(109, 222, 255, 0.32), transparent 68%);
  filter: blur(4px);
}

.person-avatar__body,
.record-row__avatar {
  position: relative;
  display: grid;
  place-items: center;
  color: #dbf8ff;
  background:
    linear-gradient(180deg, rgba(109, 200, 255, 0.24), rgba(255, 255, 255, 0.02)),
    linear-gradient(180deg, #162a48 0%, #0c1730 100%);
  border: 1px solid rgba(112, 222, 255, 0.28);
}

.person-avatar__image,
.record-row__avatar-image {
  position: relative;
  width: 100%;
  height: 100%;
  object-fit: cover;
  border: 1px solid rgba(112, 222, 255, 0.28);
  background:
    linear-gradient(180deg, rgba(109, 200, 255, 0.18), rgba(255, 255, 255, 0.02)),
    linear-gradient(180deg, #162a48 0%, #0c1730 100%);
}

.person-avatar__body {
  width: 70px;
  height: 70px;
  border-radius: 22px;
  font-size: 22px;
  font-weight: 700;
}

.person-avatar__image {
  width: 70px;
  height: 70px;
  border-radius: 22px;
}

.person-summary {
  display: grid;
  align-content: center;
  gap: 3px;
}

.person-summary__top {
  display: flex;
  align-items: center;
  gap: 10px;
}

.person-summary p {
  font-size: 12px;
  color: rgba(216, 239, 255, 0.8);
}

.person-alert-tag,
.record-warn-badge,
.stay-status--warn {
  font-style: normal;
  font-size: 11px;
  color: #ffd0d0;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(138, 31, 31, 0.28);
  border: 1px solid rgba(255, 113, 113, 0.22);
}

.person-info-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  align-content: start;
}

.person-info-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 9px 10px;
  border-radius: 14px;
  background: rgba(14, 37, 67, 0.72);
  border: 1px solid rgba(92, 201, 255, 0.12);
}

.person-info-row span,
.record-row__main span,
.stay-row__dept,
.stay-row__location,
.footer__item span {
  font-size: 12px;
  color: #8fdfff;
}

.person-info-row strong,
.footer__item strong {
  color: #f0fbff;
}

.record-row,
.stay-row {
  width: 100%;
  color: #f0fbff;
  cursor: pointer;
  text-align: left;
  transition:
    border-color 0.2s ease,
    transform 0.2s ease,
    box-shadow 0.2s ease;
}

.record-row {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr) 92px;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid rgba(92, 201, 255, 0.12);
  border-radius: 18px;
  background: rgba(14, 37, 67, 0.9);
}

.stay-table-head {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.15fr) minmax(0, 1.65fr) minmax(0, 0.95fr);
  gap: 10px;
  padding: 0 12px 2px;
  font-size: 11px;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: #78d9ff;
}

.stay-row {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.15fr) minmax(0, 1.65fr) minmax(0, 0.95fr);
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid rgba(92, 201, 255, 0.12);
  border-radius: 18px;
  background: rgba(14, 37, 67, 0.9);
}

.stay-table-head span:nth-child(4) {
  text-align: right;
}

.record-row--warn,
.stay-row--warn {
  border-color: rgba(255, 113, 113, 0.24);
  box-shadow: 0 0 22px rgba(255, 89, 89, 0.12);
}

.record-row--active,
.stay-row--active {
  border-color: rgba(92, 201, 255, 0.42);
  box-shadow: 0 0 24px rgba(61, 185, 255, 0.16);
}

.record-row__avatar {
  width: 50px;
  height: 50px;
  border-radius: 18px;
  font-size: 16px;
  font-weight: 700;
  overflow: hidden;
}

.record-row__main {
  display: grid;
  gap: 4px;
}

.record-row__main strong,
.stay-row__name {
  font-size: 14px;
}

.stay-row__name,
.stay-row__dept,
.stay-row__location,
.stay-row__meta {
  min-width: 0;
}

.stay-row__location {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.record-row__time,
.stay-row__meta {
  display: grid;
  justify-items: end;
  gap: 5px;
  text-align: right;
}

.record-row__time {
  font-size: 15px;
  color: #6ee8ff;
}

.record-row__tags {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 6px;
}

.record-direction-badge {
  font-style: normal;
  font-size: 11px;
  color: #d9fbff;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(44, 139, 255, 0.12);
  border: 1px solid rgba(91, 230, 255, 0.14);
}

.record-direction-badge--in {
  color: #d7ffe9;
  background: rgba(41, 176, 104, 0.2);
  border-color: rgba(117, 255, 176, 0.22);
}

.record-direction-badge--out {
  color: #ffe6d8;
  background: rgba(210, 100, 56, 0.22);
  border-color: rgba(255, 169, 118, 0.22);
}

.stay-row__meta strong {
  font-size: 15px;
  color: #89ff79;
}

.stay-row__meta em {
  font-style: normal;
  font-size: 11px;
  color: #8ff5ff;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(29, 141, 255, 0.12);
  border: 1px solid rgba(91, 230, 255, 0.14);
}

.empty-state {
  display: grid;
  place-items: center;
  min-height: 96px;
  border: 1px dashed rgba(92, 201, 255, 0.16);
  border-radius: 18px;
  color: rgba(216, 239, 255, 0.76);
  background: rgba(10, 30, 56, 0.38);
  font-size: 13px;
}

.footer {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  align-items: center;
  padding-block: 10px;
}

.footer__item {
  display: grid;
  gap: 4px;
  justify-items: center;
  text-align: center;
}

.footer__item--time strong {
  font-size: 24px;
  color: #6ee8ff;
  letter-spacing: 0.06em;
}

.records-list::-webkit-scrollbar,
.stay-list::-webkit-scrollbar,
.alarm-list::-webkit-scrollbar,
.abnormal-window__list::-webkit-scrollbar,
.abnormal-window__payload::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

.records-list::-webkit-scrollbar-track,
.stay-list::-webkit-scrollbar-track,
.alarm-list::-webkit-scrollbar-track,
.abnormal-window__list::-webkit-scrollbar-track,
.abnormal-window__payload::-webkit-scrollbar-track {
  border-radius: 999px;
  background: rgba(10, 35, 58, 0.9);
}

.records-list::-webkit-scrollbar-thumb,
.stay-list::-webkit-scrollbar-thumb,
.alarm-list::-webkit-scrollbar-thumb,
.abnormal-window__list::-webkit-scrollbar-thumb,
.abnormal-window__payload::-webkit-scrollbar-thumb {
  border-radius: 999px;
  background: linear-gradient(180deg, #5be6ff 0%, #1d8dff 100%);
}

@keyframes area-alert-overlay {
  0% { opacity: 0.28; }
  100% { opacity: 0.72; }
}

@keyframes area-alert-banner {
  0% { transform: scale(1); box-shadow: 0 0 18px rgba(255, 61, 61, 0.18); }
  100% { transform: scale(1.004); box-shadow: 0 0 44px rgba(255, 61, 61, 0.34); }
}

@media (max-width: 1366px) {
  .screen {
    gap: 10px;
    padding: 10px;
  }

  .content-grid {
    grid-template-columns: minmax(0, 1.12fr) minmax(320px, 0.88fr);
  }

  .person-info-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 1180px) {
  .screen {
    grid-template-rows: auto auto auto auto;
    overflow: visible;
  }

  .topbar {
    align-items: start;
    flex-direction: column;
  }

  .topbar__meta {
    width: 100%;
    justify-content: flex-start;
  }

  .content-grid {
    grid-template-columns: 1fr;
  }

  .abnormal-window {
    position: static;
    width: 100%;
    max-height: none;
    transform: none;
  }

  .left-stack,
  .right-stack {
    grid-template-rows: auto auto auto;
  }

  .capacity-control__form,
  .pass-rule-card {
    grid-template-columns: 1fr;
  }

  .pass-rule-card__tags {
    justify-content: flex-start;
  }

  .records-panel,
  .stay-panel,
  .person-panel {
    min-height: 360px;
  }
}

@media (max-width: 1024px) and (orientation: portrait) {
  .metrics-grid,
  .person-info-grid,
  .footer {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .stay-row,
  .stay-table-head {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 820px) {
  .screen {
    padding: 10px;
  }

  .metrics-grid,
  .person-info-grid,
  .footer {
    grid-template-columns: 1fr;
  }

  .person-card,
  .record-row,
  .alarm-card,
  .stay-row,
  .stay-table-head {
    grid-template-columns: 1fr;
  }

  .alarm-card__top,
  .person-summary__top,
  .abnormal-window__card-top {
    flex-direction: column;
    align-items: start;
  }

  .capacity-control__form {
    grid-template-columns: 1fr;
  }

  .capacity-control__button {
    width: 100%;
  }

  .record-row__time,
  .stay-row__meta {
    justify-items: start;
    text-align: left;
  }

  .stay-table-head {
    display: none;
  }
}
</style>
