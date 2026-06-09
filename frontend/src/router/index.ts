import { createRouter, createWebHistory } from 'vue-router'
import ChannelOverview from '../views/ChannelOverview.vue'
import ChannelDashboard from '../views/ChannelDashboard.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'overview',
      component: ChannelOverview,
    },
    {
      path: '/channel/:channelId',
      name: 'channel',
      component: ChannelDashboard,
      props: true,
    },
  ],
})

export default router
