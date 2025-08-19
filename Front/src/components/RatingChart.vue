<template>
  <div class="rating-chart">
    <h4>Avaliação de Código Limpo</h4>
    <div class="rating-items">
      <div class="rating-item" v-for="(value, key) in ratingItems" :key="key">
        <div class="rating-label">
          <span>{{ value.label }}</span>
          <span class="rating-score">{{ value.score }}/10</span>
        </div>
        <div class="score-bar">
          <div class="score-fill" :style="{ width: (value.score * 10) + '%', backgroundColor: getScoreColor(value.score) }"></div>
        </div>
        <div class="justification" v-if="value.justification">
          <small>{{ value.justification }}</small>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'RatingChart',
  props: {
    rating: {
      type: Object,
      required: true
    }
  },
  computed: {
    ratingItems() {
      return {
        variableNaming: {
          label: 'Nomenclatura de Variáveis',
          score: this.rating.variableNaming,
          justification: this.rating.justifies.variableNaming
        },
        functionSizes: {
          label: 'Tamanho das Funções',
          score: this.rating.functionSizes,
          justification: this.rating.justifies.functionSizes
        },
        noNeedsComments: {
          label: 'Código Autoexplicativo',
          score: this.rating.noNeedsComments,
          justification: this.rating.justifies.noNeedsComments
        },
        methodCohesion: {
          label: 'Coesão dos Métodos',
          score: this.rating.methodCohesion,
          justification: this.rating.justifies.methodCohesion
        },
        deadCode: {
          label: 'Código Morto',
          score: this.rating.deadCode,
          justification: this.rating.justifies.deadCode
        }
      };
    }
  },
  methods: {
    getScoreColor(score) {
      if (score >= 9) return '#28a745';
      if (score >= 7) return '#20c997';
      if (score >= 6) return '#17a2b8';
      if (score >= 5) return '#ffc107';
      if (score >= 3.5) return '#fd7e14';
      return '#dc3545';
    }
  }
};
</script>

<style scoped>
.rating-chart {
  margin-bottom: 2rem;
}

.rating-chart h4 {
  margin-bottom: 1rem;
  color: #333;
  font-size: 1.1rem;
}

.rating-items {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.rating-item {
  background-color: #f8f9fa;
  padding: 1rem;
  border-radius: 8px;
  border-left: 4px solid #007bff;
}

.rating-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
  font-weight: 500;
}

.rating-score {
  background-color: #007bff;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: bold;
}

.score-bar {
  width: 100%;
  height: 8px;
  background-color: #e9ecef;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.score-fill {
  height: 100%;
  transition: width 0.3s ease;
}

.justification {
  color: #666;
  font-style: italic;
  line-height: 1.4;
}

.justification small {
  font-size: 0.85rem;
}
</style>
