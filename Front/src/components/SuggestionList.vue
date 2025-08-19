<template>
  <div class="suggestion-list">
    <div v-for="suggestion in suggestions" :key="suggestion.title" class="suggestion-item">
      <div class="suggestion-header">
        <h5 class="suggestion-title">{{ suggestion.title }}</h5>
        <div class="suggestion-badges">
          <span class="badge priority" :style="{ backgroundColor: suggestion.getPriorityColor() }">
            {{ suggestion.priority }}
          </span>
          <span class="badge type">{{ suggestion.type }}</span>
          <span class="badge difficulty" :style="{ backgroundColor: suggestion.getDifficultyColor() }">
            {{ suggestion.difficult }}
          </span>
        </div>
      </div>

      <p class="suggestion-description">{{ suggestion.description }}</p>

      <div class="suggestion-meta">
        <span class="file-reference">{{ suggestion.fileReference }}</span>
        <span class="last-update">{{ formatDate(suggestion.lastUpdate) }}</span>
      </div>

      <div v-if="suggestion.studyResources.length > 0" class="study-resources">
        <h6>Recursos de Estudo:</h6>
        <ul>
          <li v-for="resource in suggestion.studyResources" :key="resource">
            <a :href="resource" target="_blank" rel="noopener">{{ resource }}</a>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'SuggestionList',
  props: {
    suggestions: {
      type: Array,
      required: true
    }
  },
  methods: {
    formatDate(dateString) {
      return new Date(dateString).toLocaleDateString('pt-BR');
    }
  }
};
</script>

<style scoped>
.suggestion-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.suggestion-item {
  background-color: white;
  border-radius: 8px;
  padding: 1rem;
  border-left: 4px solid #ffc107;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.suggestion-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1rem;
  gap: 1rem;
}

.suggestion-title {
  margin: 0;
  color: #333;
  font-size: 1rem;
  flex: 1;
}

.suggestion-badges {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.badge {
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: bold;
  text-transform: uppercase;
  color: white;
}

.badge.priority {
  background-color: #dc3545;
}

.badge.type {
  background-color: #6c757d;
}

.badge.difficulty {
  background-color: #fd7e14;
}

.suggestion-description {
  color: #666;
  line-height: 1.5;
  margin-bottom: 1rem;
}

.suggestion-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  font-size: 0.85rem;
  color: #999;
}

.file-reference {
  background-color: #f8f9fa;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-family: monospace;
}

.study-resources {
  border-top: 1px solid #e9ecef;
  padding-top: 1rem;
}

.study-resources h6 {
  margin: 0 0 0.5rem 0;
  color: #333;
  font-size: 0.9rem;
}

.study-resources ul {
  margin: 0;
  padding-left: 1.5rem;
}

.study-resources li {
  margin-bottom: 0.25rem;
}

.study-resources a {
  color: #007bff;
  text-decoration: none;
}

.study-resources a:hover {
  text-decoration: underline;
}

@media (max-width: 768px) {
  .suggestion-header {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .suggestion-badges {
    margin-top: 0.5rem;
  }
  
  .suggestion-meta {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }
}
</style>
