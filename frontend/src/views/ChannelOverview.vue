<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { buildApiUrl } from '../utils/api'

type ChannelConfigItem = {
  id: string
  name: string
  limitCount: number
  deviceCount: number
}

type ChannelDeviceStatusItem = {
  ip: string
  name: string
  deviceName: string
  direction: string
  online: boolean
  status: string
}

type ChannelOverviewItem = {
  id: string
  name: string
  limitCount: number
  enterCount: number
  exitCount: number
  stayCount: number
  alarmCount: number
  accessRuleMode: 'normal' | 'warning' | 'exit-only' | 'blocked'
  deviceCount: number
  onlineDeviceCount: number
  devices: ChannelDeviceStatusItem[]
}

type ChannelOverviewResponse = {
  generatedAt?: string
  channels?: ChannelOverviewItem[]
}

const systemTitle = '限员管控系统'
const router = useRouter()
const channels = ref<ChannelConfigItem[]>([])
const channelOverview = ref<ChannelOverviewItem[]>([])
const overviewError = ref('')
const overviewUpdatedAt = ref('--')
let overviewTimer: number | undefined

const channelAccessRuleLabels: Record<string, { title: string; summary: string }> = {
  normal: { title: '正常通行', summary: '可正常进出' },
  warning: { title: '临界预警', summary: '距离限员仅差 1 人' },
  'exit-only': { title: '只出不进', summary: '已达限员人数' },
  blocked: { title: '超员管控', summary: '已超出限制人数' },
}

function formatChannelAccessRule(mode: string) {
  return channelAccessRuleLabels[mode] ?? channelAccessRuleLabels.normal
}

function formatDeviceLabel(device: ChannelDeviceStatusItem) {
  return device.deviceName || device.name || device.ip
}

function formatChannelOnlineSummary(channel: ChannelOverviewItem) {
  if (!channel.deviceCount) {
    return '未配置门禁'
  }
  return `${channel.onlineDeviceCount}/${channel.deviceCount} 在线`
}

function hasOfflineDevices(channel: ChannelOverviewItem) {
  return channel.deviceCount > 0 && channel.onlineDeviceCount < channel.deviceCount
}

async function loadChannels() {
  try {
    const response = await fetch(buildApiUrl('/api/channels'), {
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
      throw new Error(`通道列表接口返回 ${response.status}`)
    }
    channels.value = (await response.json()) as ChannelConfigItem[]
  } catch (error) {
    overviewError.value = error instanceof Error ? error.message : '加载通道列表失败'
  }
}

async function loadChannelOverview() {
  try {
    const response = await fetch(buildApiUrl('/api/channels/overview'), {
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
      throw new Error(`通道总览接口返回 ${response.status}`)
    }
    const payload = (await response.json()) as ChannelOverviewResponse
    channelOverview.value = payload.channels ?? []
    overviewUpdatedAt.value = payload.generatedAt || new Date().toLocaleString()
    overviewError.value = ''
  } catch (error) {
    overviewError.value = error instanceof Error ? error.message : '加载通道总览失败'
  }
}

function startOverviewPolling() {
  if (overviewTimer) {
    return
  }

  overviewTimer = window.setInterval(() => {
    void loadChannelOverview()
  }, 8000)
}

function stopOverviewPolling() {
  if (overviewTimer) {
    window.clearInterval(overviewTimer)
    overviewTimer = undefined
  }
}

function openChannel(channel: ChannelOverviewItem) {
  router.push({ name: 'channel', params: { channelId: channel.id } })
}

onMounted(() => {
  void loadChannels()
  void loadChannelOverview()
  startOverviewPolling()
})

onBeforeUnmount(() => {
  stopOverviewPolling()
})
</script>

<template>
  <main class="screen">
    <header class="topbar panel">
      <div>
        <p class="topbar__tag">PERSONNEL ACCESS CONTROL PLATFORM</p>
        <h1>{{ systemTitle }}</h1>
      </div>
      <div class="topbar__meta">
        <span class="hint-chip">共 {{ channelOverview.length || channels.length }} 个通道</span>
      </div>
    </header>

    <section class="channel-overview panel">
      <div class="panel-head">
        <div>
          <p class="eyebrow">CHANNEL OVERVIEW</p>
          <h2>通道总览</h2>
        </div>
        <span class="hint-chip">最后更新 {{ overviewUpdatedAt }}</span>
      </div>

      <p v-if="overviewError" class="channel-overview__error">{{ overviewError }}</p>

      <div class="channel-overview__grid">
        <button
          v-for="channel in channelOverview"
          :key="channel.id"
          class="channel-card"
          :class="[
            `channel-card--${channel.accessRuleMode}`,
            { 'channel-card--device-offline': hasOfflineDevices(channel) },
          ]"
          type="button"
          @click="openChannel(channel)"
        >
          <div class="channel-card__head">
            <div>
              <p class="eyebrow">ACCESS CHANNEL</p>
              <h3>{{ channel.name }}</h3>
            </div>
            <span class="channel-card__badge" :class="`channel-card__badge--${channel.accessRuleMode}`">
              {{ formatChannelAccessRule(channel.accessRuleMode).title }}
            </span>
          </div>

          <div class="channel-card__metrics">
            <div class="channel-card__metric">
              <span>停留人数</span>
              <strong>{{ channel.stayCount }}</strong>
            </div>
            <div class="channel-card__metric">
              <span>限制人数</span>
              <strong>{{ channel.limitCount }}</strong>
            </div>
            <div class="channel-card__metric">
              <span>进场</span>
              <strong>{{ channel.enterCount }}</strong>
            </div>
            <div class="channel-card__metric">
              <span>出场</span>
              <strong>{{ channel.exitCount }}</strong>
            </div>
          </div>

          <div v-if="channel.devices?.length" class="channel-card__devices">
            <div class="channel-card__devices-head">
              <span>门禁在线状态</span>
              <strong :class="{ 'channel-card__online--warn': hasOfflineDevices(channel) }">
                {{ formatChannelOnlineSummary(channel) }}
              </strong>
            </div>
            <div
              v-for="device in channel.devices"
              :key="`${channel.id}-${device.ip}`"
              class="device-status"
              :class="{ 'device-status--online': device.online, 'device-status--offline': !device.online }"
            >
              <span class="device-status__dot"></span>
              <div class="device-status__main">
                <strong>{{ formatDeviceLabel(device) }}</strong>
                <span>{{ device.ip }} · {{ device.direction }}</span>
              </div>
              <em>{{ device.status }}</em>
            </div>
          </div>

          <div class="channel-card__footer">
            <span>{{ formatChannelAccessRule(channel.accessRuleMode).summary }}</span>
            <span>
              {{ formatChannelOnlineSummary(channel) }}
              <template v-if="channel.alarmCount"> · {{ channel.alarmCount }} 条报警</template>
            </span>
          </div>
        </button>
      </div>

      <div v-if="!channelOverview.length && !overviewError" class="empty-state">正在加载通道数据...</div>
    </section>
  </main>
</template>

<style scoped>
.screen {
  min-height: 100vh;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.panel {
  border: 1px solid rgba(88, 196, 255, 0.22);
  border-radius: 20px;
  background:
    linear-gradient(180deg, rgba(10, 34, 58, 0.92), rgba(5, 18, 34, 0.92)),
    radial-gradient(circle at top right, rgba(70, 183, 255, 0.16), transparent 48%);
  box-shadow: 0 10px 28px rgba(0, 0, 0, 0.22);
}

.topbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  padding: 16px 20px;
}

.topbar__tag {
  margin: 0;
  font-size: 11px;
  letter-spacing: 0.18em;
  color: rgba(143, 214, 255, 0.72);
}

.topbar h1 {
  margin: 6px 0 0;
  font-size: 1.8rem;
}

.topbar__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
}

.hint-chip {
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 12px;
  color: rgba(216, 239, 255, 0.86);
  background: rgba(8, 34, 58, 0.72);
  border: 1px solid rgba(88, 196, 255, 0.22);
}

.panel-head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: start;
}

.eyebrow {
  margin: 0;
  font-size: 11px;
  letter-spacing: 0.16em;
  color: rgba(143, 214, 255, 0.72);
}

.panel-head h2 {
  margin: 4px 0 0;
}

.channel-overview {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 18px;
}

.channel-overview__error {
  margin: 0;
  color: #ff9f9f;
}

.channel-overview__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  min-height: 0;
  flex: 1;
}

.channel-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-height: 220px;
  padding: 18px;
  border-radius: 18px;
  border: 1px solid rgba(88, 196, 255, 0.22);
  background: rgba(4, 16, 30, 0.55);
  color: inherit;
  text-align: left;
  cursor: pointer;
  transition: transform 0.2s ease, border-color 0.2s ease, box-shadow 0.2s ease;
}

.channel-card:hover {
  transform: translateY(-2px);
  border-color: rgba(110, 216, 255, 0.55);
  box-shadow: 0 14px 30px rgba(0, 0, 0, 0.28);
}

.channel-card__head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: start;
}

.channel-card__head h3 {
  margin: 4px 0 0;
  font-size: 1.45rem;
}

.channel-card__badge {
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 0.82rem;
  white-space: nowrap;
  border: 1px solid rgba(88, 196, 255, 0.28);
  background: rgba(8, 34, 58, 0.72);
}

.channel-card__badge--warning {
  color: #ffd27a;
  border-color: rgba(255, 196, 88, 0.45);
}

.channel-card__badge--exit-only {
  color: #ffb27a;
  border-color: rgba(255, 148, 88, 0.45);
}

.channel-card__badge--blocked {
  color: #ff8f8f;
  border-color: rgba(255, 96, 96, 0.5);
}

.channel-card__metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.channel-card__metric {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px;
  border-radius: 12px;
  background: rgba(4, 16, 30, 0.55);
  border: 1px solid rgba(88, 196, 255, 0.12);
}

.channel-card__metric span {
  color: rgba(173, 220, 255, 0.72);
  font-size: 0.82rem;
}

.channel-card__metric strong {
  font-size: 1.5rem;
  color: #eff9ff;
}

.channel-card__footer {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  color: rgba(173, 220, 255, 0.78);
  font-size: 0.88rem;
}

.channel-card--warning {
  border-color: rgba(255, 196, 88, 0.35);
}

.channel-card--exit-only {
  border-color: rgba(255, 148, 88, 0.38);
}

.channel-card--blocked {
  border-color: rgba(255, 96, 96, 0.45);
  box-shadow: inset 0 0 0 1px rgba(255, 96, 96, 0.12);
}

.channel-card--device-offline {
  border-color: rgba(255, 120, 96, 0.42);
}

.channel-card__devices {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.channel-card__devices-head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
  color: rgba(173, 220, 255, 0.78);
  font-size: 0.84rem;
}

.channel-card__devices-head strong {
  color: #8ff5a8;
  font-size: 0.88rem;
}

.channel-card__online--warn {
  color: #ff9f9f !important;
}

.device-status {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 10px;
  align-items: center;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid rgba(88, 196, 255, 0.12);
  background: rgba(4, 16, 30, 0.55);
}

.device-status__dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #ff7a7a;
  box-shadow: 0 0 10px rgba(255, 96, 96, 0.45);
}

.device-status--online .device-status__dot {
  background: #6dff8f;
  box-shadow: 0 0 10px rgba(109, 255, 143, 0.45);
}

.device-status__main {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.device-status__main strong {
  font-size: 0.92rem;
  color: #eff9ff;
}

.device-status__main span {
  font-size: 0.78rem;
  color: rgba(173, 220, 255, 0.68);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.device-status em {
  font-style: normal;
  font-size: 0.78rem;
  padding: 4px 8px;
  border-radius: 999px;
  white-space: nowrap;
}

.device-status--online em {
  color: #8ff5a8;
  background: rgba(36, 120, 64, 0.28);
  border: 1px solid rgba(109, 255, 143, 0.24);
}

.device-status--offline em {
  color: #ff9f9f;
  background: rgba(120, 36, 36, 0.28);
  border: 1px solid rgba(255, 96, 96, 0.24);
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

@media (max-width: 820px) {
  .screen {
    padding: 10px;
  }

  .channel-overview__grid {
    grid-template-columns: 1fr;
  }

  .channel-card__metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .channel-card__footer {
    flex-direction: column;
  }
}
</style>
