import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import CommitAnalysisList from '@/views/CommitAnalysisList.vue'

const routes = [
  {
    path: '/',
    name: 'home',
    component: HomeView
  },
  {
    path: '/analyses',
    name: 'analyses',
    component: CommitAnalysisList
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
