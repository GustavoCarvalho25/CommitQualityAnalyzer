import { CleanCodeRating } from './CleanCodeRating.js'
import { Suggestion } from './Suggestion.js'

export class CommitFile {
    constructor(data) {
        this.id = data.id;
        this.path = data.path;
        this.language = data.language;
        this.addedLines = data.addedLines;
        this.removedLines = data.removedLines;
        this.content = data.content;
        this.hasAnalysis = data.hasAnalysis;
        this.rating = data.rating ? new CleanCodeRating(data.rating) : null;
        this.suggestions = data.suggestions.map(suggestion => new Suggestion(suggestion));
    }

    getFileName() {
        return this.path.split('/').pop();
    }

    getFileExtension() {
        const fileName = this.getFileName();
        return fileName.includes('.') ? fileName.split('.').pop() : '';
    }
}
