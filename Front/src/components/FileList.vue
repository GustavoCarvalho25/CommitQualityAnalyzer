<template>
  <div class="file-list">
    <div v-for="file in files" :key="file.id" class="file-item">
      <div class="file-header">
        <div class="file-info">
          <span class="file-name">{{ file.getFileName() }}</span>
          <span class="file-extension">{{ file.getFileExtension() }}</span>
        </div>
        <div class="file-stats">
          <span class="lines-added">+{{ file.addedLines }}</span>
          <span class="lines-removed">-{{ file.removedLines }}</span>
        </div>
      </div>

      <div v-if="file.hasAnalysis" class="file-rating">
        <div class="file-score">
          <span class="score">{{ file.rating.note.toFixed(1) }}</span>
          <span class="quality">{{ file.rating.quality }}</span>
        </div>
        <RatingChart :rating="file.rating" />
      </div>

      <div v-if="file.suggestions.length > 0" class="file-suggestions">
        <SuggestionList :suggestions="file.suggestions" />
      </div>
    </div>
  </div>
</template>

<script>
import RatingChart from './RatingChart.vue';
import SuggestionList from './SuggestionList.vue';

export default {
  name: 'FileList',
  components: {
    RatingChart,
    SuggestionList
  },
  props: {
    files: {
      type: Array,
      required: true
    }
  }
};
</script>

<style scoped>
.file-list {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.file-item {
  background-color: #f8f9fa;
  border-radius: 8px;
  padding: 1rem;
  border-left: 3px solid #007bff;
}

.file-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #e9ecef;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.file-name {
  font-weight: 600;
  color: #333;
}

.file-extension {
  background-color: #007bff;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: bold;
}

.file-stats {
  display: flex;
  gap: 0.5rem;
}

.lines-added {
  color: #28a745;
  font-weight: bold;
}

.lines-removed {
  color: #dc3545;
  font-weight: bold;
}

.file-rating {
  margin-bottom: 1rem;
}

.file-score {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.score {
  background-color: #007bff;
  color: white;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-weight: bold;
  font-size: 1.1rem;
}

.quality {
  background-color: #6c757d;
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  text-transform: uppercase;
  font-weight: 600;
}

.file-suggestions {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e9ecef;
}
</style>
