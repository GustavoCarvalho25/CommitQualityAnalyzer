<template>
  <div class="commit-analysis-card" :style="{ borderColor: analysis.getQualityColor() }">
    <div class="card-header">
      <div class="commit-info">
        <h3 class="commit-id">{{ analysis.commitId }}</h3>
        <p class="author">{{ analysis.author }}</p>
        <p class="date">{{ formatDate(analysis.commitDate) }}</p>
      </div>
      <div class="score-section">
        <div class="overall-score" :style="{ backgroundColor: analysis.getQualityColor() }">
          {{ analysis.overallNote.toFixed(1) }}
        </div>
        <div class="quality-label">{{ analysis.rating.quality }}</div>
      </div>
    </div>

    <div class="card-body">
      <div class="stats">
        <div class="stat-item">
          <span class="label">Linhas Adicionadas:</span>
          <span class="value added">+{{ analysis.addedLines }}</span>
        </div>
        <div class="stat-item">
          <span class="label">Linhas Removidas:</span>
          <span class="value removed">-{{ analysis.removedLines }}</span>
        </div>
        <div class="stat-item">
          <span class="label">Linguagem:</span>
          <span class="value">{{ analysis.language }}</span>
        </div>
      </div>

      <RatingChart :rating="analysis.rating" />

      <div class="files-section">
        <h4>Arquivos Analisados ({{ analysis.files.length }})</h4>
        <FileList :files="analysis.files" />
      </div>

      <div class="suggestions-section" v-if="analysis.suggestions.length > 0">
        <h4>Sugestões de Melhoria ({{ analysis.suggestions.length }})</h4>
        <SuggestionList :suggestions="analysis.suggestions" />
      </div>
    </div>
  </div>
</template>

<script>
import RatingChart from './RatingChart.vue';
import FileList from './FileList.vue';
import SuggestionList from './SuggestionList.vue';

export default {
  name: 'CommitAnalysisCard',
  components: {
    RatingChart,
    FileList,
    SuggestionList
  },
  props: {
    analysis: {
      type: Object,
      required: true
    }
  },
  methods: {
    formatDate(dateString) {
      return new Date(dateString).toLocaleDateString('pt-BR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    }
  }
};
</script>

<style scoped>
.commit-analysis-card {
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 6px rgba(0,0,0,0.1);
  border-left: 4px solid;
  overflow: hidden;
  transition: transform 0.3s, box-shadow 0.3s;
}

.commit-analysis-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 12px rgba(0,0,0,0.15);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1.5rem;
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
}

.commit-info h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.2rem;
  color: #333;
}

.author {
  margin: 0 0 0.25rem 0;
  color: #666;
  font-weight: 500;
}

.date {
  margin: 0;
  color: #999;
  font-size: 0.9rem;
}

.score-section {
  text-align: center;
}

.overall-score {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-weight: bold;
  font-size: 1.2rem;
  margin-bottom: 0.5rem;
}

.quality-label {
  font-size: 0.8rem;
  color: #666;
  text-transform: uppercase;
  font-weight: 600;
}

.card-body {
  padding: 1.5rem;
}

.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
}

.stat-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background-color: #f8f9fa;
  border-radius: 6px;
}

.label {
  font-weight: 500;
  color: #666;
}

.value {
  font-weight: bold;
}

.value.added {
  color: #28a745;
}

.value.removed {
  color: #dc3545;
}

.files-section,
.suggestions-section {
  margin-top: 2rem;
}

.files-section h4,
.suggestions-section h4 {
  margin-bottom: 1rem;
  color: #333;
  font-size: 1.1rem;
  border-bottom: 2px solid #e9ecef;
  padding-bottom: 0.5rem;
}
</style>
