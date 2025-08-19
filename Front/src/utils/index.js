// utils/formatters.js
export const formatters = {
    formatDate(dateString) {
        return new Date(dateString).toLocaleDateString('pt-BR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    },

    formatScore(score) {
        return score.toFixed(1);
    },

    getQualityColor(quality) {
        const colors = {
            'Excellent': '#28a745',
            'VeryGood': '#20c997',
            'Good': '#17a2b8',
            'Acceptable': '#ffc107',
            'NeedsImprovement': '#fd7e14',
            'Problematic': '#dc3545'
        };
        return colors[quality] || '#6c757d';
    },

    getScoreColor(score) {
        if (score >= 9) return '#28a745';
        if (score >= 7) return '#20c997';
        if (score >= 6) return '#17a2b8';
        if (score >= 5) return '#ffc107';
        if (score >= 3.5) return '#fd7e14';
        return '#dc3545';
    }
};

// utils/validators.js
export const validators = {
    isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    },

    isValidCommitId(commitId) {
        return commitId && commitId.length >= 7;
    },

    isValidDate(dateString) {
        const date = new Date(dateString);
        return date instanceof Date && !isNaN(date);
    }
};

// Exportação padrão
export default {
    formatters,
    validators
};
