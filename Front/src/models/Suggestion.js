export class Suggestion {
    constructor(data) {
        this.title = data.title;
        this.description = data.description;
        this.priority = data.priority;
        this.type = data.type;
        this.difficult = data.difficult;
        this.fileReference = data.fileReference;
        this.lastUpdate = data.lastUpdate;
        this.studyResources = data.studyResources;
    }

    getPriorityColor() {
        const colors = {
            'High': '#dc3545',
            'Medium': '#ffc107',
            'Low': '#28a745'
        };
        return colors[this.priority] || '#6c757d';
    }

    getDifficultyColor() {
        const colors = {
            'Easy': '#28a745',
            'Medium': '#ffc107',
            'Hard': '#dc3545'
        };
        return colors[this.difficult] || '#6c757d';
    }
}
