<template>
  <div class="commit-analysis-list">
    <div class="page-header">
      <h1>Análises de Commits</h1>
      <div class="filters">
        <select v-model="selectedQuality" class="filter-select">
          <option value="">Todas as Qualidades</option>
          <option value="Excellent">Excelente</option>
          <option value="VeryGood">Muito Bom</option>
          <option value="Good">Bom</option>
          <option value="Acceptable">Aceitável</option>
          <option value="NeedsImprovement">Precisa Melhorar</option>
          <option value="Problematic">Problemático</option>
        </select>
      </div>
    </div>

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
      <p>Carregando análises...</p>
    </div>

    <div v-else-if="error" class="error">
      <p>{{ error }}</p>
      <button @click="fetchAnalyses" class="retry-button">Tentar novamente</button>
    </div>

    <div v-else>
      <div v-if="filteredAnalyses.length === 0" class="no-results">
        <p>Nenhuma análise encontrada.</p>
      </div>
      
      <div v-else class="analyses-grid">
        <div v-for="analysis in filteredAnalyses" :key="analysis.id" class="analysis-item">
          <CommitAnalysisCard :analysis="analysis" />
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { useCommitAnalysisStore } from '@/stores/commitAnalysis';
import CommitAnalysisCard from '@/components/CommitAnalysisCard.vue';
import { computed, ref } from 'vue';

export default {
  name: 'CommitAnalysisList',
  components: {
    CommitAnalysisCard
  },
  setup() {
    const store = useCommitAnalysisStore();
    const selectedQuality = ref('');

    const filteredAnalyses = computed(() => {
      if (!selectedQuality.value) {
        return store.commitAnalyses;
      }
      return store.commitAnalyses.filter(analysis => 
        analysis.rating.quality === selectedQuality.value
      );
    });

    return {
      commitAnalyses: store.commitAnalyses,
      loading: store.loading,
      error: store.error,
      fetchAnalyses: store.fetchCommitAnalyses,
      selectedQuality,
      filteredAnalyses
    };
  },
  mounted() {
    this.fetchAnalyses();
  }
};
</script>

<style scoped>
.commit-analysis-list {
  max-width: 1200px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 2px solid #e9ecef;
}

.page-header h1 {
  margin: 0;
  color: #333;
}

.filters {
  display: flex;
  gap: 1rem;
}

.filter-select {
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  background-color: white;
}

.loading {
  text-align: center;
  padding: 3rem;
}

.spinner {
  border: 4px solid #f3f3f3;
  border-top: 4px solid #667eea;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  animation: spin 1s linear infinite;
  margin: 0 auto 1rem;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.error {
  text-align: center;
  padding: 2rem;
  background-color: #f8d7da;
  border: 1px solid #f5c6cb;
  border-radius: 8px;
  color: #721c24;
}

.retry-button {
  background-color: #dc3545;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  margin-top: 1rem;
}

.retry-button:hover {
  background-color: #c82333;
}

.no-results {
  text-align: center;
  padding: 3rem;
  color: #666;
}

.analyses-grid {
  display: grid;
  gap: 2rem;
}
</style>
