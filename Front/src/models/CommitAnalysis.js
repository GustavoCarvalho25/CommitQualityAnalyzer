import { CleanCodeRating } from './CleanCodeRating.js'
import { CommitFile } from './CommitFile.js'
import { Suggestion } from './Suggestion.js'

export class CommitAnalysis {
    constructor(data) {
        this.id = data.id;
        this.commitId = data.commitId;
        this.author = data.author;
        this.email = data.email;
        this.commitDate = data.commitDate;
        this.analysisDate = data.analysisDate;
        this.language = data.language;
        this.addedLines = data.addedLines;
        this.removedLines = data.removedLines;
        this.overallNote = data.overallNote;
        this.rating = new CleanCodeRating(data.rating);
        this.files = data.files.map(file => new CommitFile(file));
        this.suggestions = data.suggestions.map(suggestion => new Suggestion(suggestion));
    }

    getQualityColor() {
        const quality = this.rating.quality;
        const colors = {
            'Excellent': '#28a745',
            'VeryGood': '#20c997',
            'Good': '#17a2b8',
            'Acceptable': '#ffc107',
            'NeedsImprovement': '#fd7e14',
            'Problematic': '#dc3545'
        };
        return colors[quality] || '#6c757d';
    }
}
