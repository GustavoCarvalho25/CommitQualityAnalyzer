import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import dados from '../../dados.json'

export const useCommitAnalysisStore = defineStore('commitAnalysis', () => {
  const commitAnalyses = ref([])
  const loading = ref(false)
  const error = ref(null)

  const fetchCommitAnalyses = async () => {
    loading.value = true
    error.value = null
    
    try {
      // Simulando uma chamada de API
      await new Promise(resolve => setTimeout(resolve, 1000))
      
      // Importando os dados do arquivo JSON
      commitAnalyses.value = dados.commitAnalyses.map(analysis => {
        return new CommitAnalysis(analysis)
      })
    } catch (err) {
      error.value = 'Erro ao carregar análises: ' + err.message
    } finally {
      loading.value = false
    }
  }

  const getAnalysisById = computed(() => {
    return (id) => commitAnalyses.value.find(analysis => analysis.id === id)
  })

  const getAnalysesByQuality = computed(() => {
    return (quality) => commitAnalyses.value.filter(analysis => analysis.rating.quality === quality)
  })

  return {
    commitAnalyses,
    loading,
    error,
    fetchCommitAnalyses,
    getAnalysisById,
    getAnalysesByQuality
  }
})

// Importando a classe CommitAnalysis
import { CommitAnalysis } from '@/models/CommitAnalysis.js'
